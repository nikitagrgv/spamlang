using System.Diagnostics;

namespace Compiler;

public class Sema
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly TypeRegistry _typeRegistry = new();
    private readonly List<Scope> _scopes = new(); // TODO: Do we need list? Or just current scope?
    private readonly List<FuncSymbol> _funcStack = new();

    public Sema(string code, IReadOnlyList<Token> tokens, Diagnostic diag)
    {
        _code = code;
        _diag = diag;
        _tokens = tokens;
    }

    public void Run(CompilationUnit unit)
    {
        Scope scope = new(null);
        unit.Scope = scope;
        PushScope(scope);

        RegisterBuiltin(scope);

        RegisterFunctionSymbols(unit);

        VisitCompilationUnit(unit);
    }

    private void VisitCompilationUnit(CompilationUnit unit)
    {
        foreach (FuncDecl fd in unit.FuncDecls)
        {
            VisitFuncDecl(fd);
        }
    }

    private void VisitFuncDecl(FuncDecl fd)
    {
        Debug.Assert(fd.Symbol != null, $"Must be registered in {nameof(RegisterFunctionSymbols)}");

        _funcStack.Add(fd.Symbol);

        Scope scope = new(CurrentScope());
        PushScope(scope);

        foreach (Param param in fd.Params)
        {
            Debug.Assert(param.Type.ResolvedType != null, $"Must be resolved in {nameof(RegisterFunctionSymbols)}");

            Type type = param.Type.ResolvedType;
            ReadOnlySpan<char> name = GetTokenValue(param.NameToken);

            ParamSymbol sym = new()
            {
                Declaration = param,
                DeclaringScope = scope,
                Type = type,
                Name = name.ToString()
            };

            param.Symbol = sym;
            RegisterSymbol(sym);
        }

        fd.Body.Scope = scope;
        VisitBlock(fd.Body);

        FuncType funcType = (FuncType)fd.Symbol.Type;
        if (funcType.ReturnType != BuiltinType.Void)
        {
            bool hasLastReturn = fd.Body.Stmts.Count > 0 && fd.Body.Stmts[^1] is StmtReturn;
            if (!hasLastReturn)
            {
                Error($"No return statement on the end of function \"{fd.Symbol.Name}\"", fd);
            }
        }

        PopScope();

        Debug.Assert(_funcStack[^1] == fd.Symbol);
        _funcStack.RemoveAt(_funcStack.Count - 1);
    }

    private void VisitBlock(Block block)
    {
        Debug.Assert(block.Scope != null, "Block scope must be set from outside");

        foreach (Stmt stmt in block.Stmts)
        {
            switch (stmt)
            {
                case Block b:
                    Scope scope = new(CurrentScope());
                    b.Scope = scope;
                    PushScope(scope);
                    VisitBlock(b);
                    PopScope();
                    break;
                case StmtAssign stmtAssign:
                    VisitStmtAssign(stmtAssign);
                    break;
                case StmtExpr stmtExpr:
                    VisitStmtExpr(stmtExpr);
                    break;
                case StmtLet stmtLet:
                    VisitStmtLet(stmtLet);
                    break;
                case StmtReturn stmtReturn:
                    VisitStmtReturn(stmtReturn);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stmt));
            }
        }
    }

    private void VisitStmtAssign(StmtAssign stmt)
    {
        // TODO: Add value categories (lvalue/rvalue). Allow use any expression as target

        VisitExpr(stmt.Target);
        VisitExpr(stmt.Value);

        Debug.Assert(stmt.Target.ResolvedType != null);
        Debug.Assert(stmt.Value.ResolvedType != null);

        if (stmt.Target is not ExprIdentifier target)
        {
            Error("Only identifiers can be used as assign target", stmt.Target);
            return;
        }

        if (target.Symbol == null)
        {
            // Already reported
            return;
        }

        switch (target.Symbol)
        {
            case FuncSymbol:
                Error($"Cannot assign to function \"{target.Symbol.Name}\"", stmt.Target);
                return;
            case TypeSymbol:
                Error($"Cannot assign to type \"{target.Symbol.Name}\"", stmt.Target);
                return;
            case ParamSymbol:
            case VariableSymbol:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        Type targetType = target.Symbol.Type;
        Type valueType = stmt.Value.ResolvedType;
        if (valueType == BuiltinType.Error)
        {
            // Already reported
            return;
        }

        stmt.Value = Adapt(stmt.Value, targetType);
    }

    private void VisitStmtExpr(StmtExpr stmt)
    {
        VisitExpr(stmt.Expr);
    }

    private void VisitStmtLet(StmtLet stmt)
    {
        Debug.Assert(stmt.Expr != null || stmt.TypeDecl != null, "Must be guaranteed by parser");

        ReadOnlySpan<char> name = GetTokenValue(stmt.NameToken);

        Type? type = null;
        if (stmt.Expr != null)
        {
            VisitExpr(stmt.Expr);
            type = stmt.Expr.ResolvedType;

            if (type == BuiltinType.Void)
            {
                Error($"Cannot assign variable \"{name}\" to void expression", stmt);
                type = BuiltinType.Error;
            }
        }

        if (stmt.TypeDecl != null)
        {
            type = ResolveType(stmt.TypeDecl);
            if (stmt.Expr != null)
            {
                stmt.Expr = Adapt(stmt.Expr, type);
            }
        }

        Debug.Assert(type != null);

        VariableSymbol sym = new()
        {
            Declaration = stmt,
            Name = name.ToString(),
            DeclaringScope = CurrentScope(),
            Type = type
        };

        stmt.Symbol = sym;
        RegisterSymbol(sym);
    }

    private void VisitStmtReturn(StmtReturn stmt)
    {
        Debug.Assert(_funcStack.Count > 0);

        if (stmt.Expr != null)
        {
            VisitExpr(stmt.Expr);
        }

        FuncSymbol currentFunc = _funcStack[^1];
        FuncType funcType = (FuncType)currentFunc.Type;
        Type returnType = funcType.ReturnType;

        if (returnType == BuiltinType.Error)
        {
            // Already reported
            return;
        }

        if (returnType == BuiltinType.Void)
        {
            if (stmt.Expr != null)
            {
                Error($"Unexpected expression in return statement. Function \"{currentFunc.Name}\" returns void",
                    stmt);
            }

            return;
        }

        if (stmt.Expr == null)
        {
            Error($"Function \"{currentFunc.Name}\" must return value", stmt);
            return;
        }

        stmt.Expr = Adapt(stmt.Expr, returnType);
    }

    private void VisitExpr(Expr expr)
    {
        switch (expr)
        {
            case ExprBinary exprBinary:
                VisitExprBinary(exprBinary);
                break;
            case ExprCall exprCall:
                VisitExprCall(exprCall);
                break;
            case ExprIdentifier exprIdentifier:
                VisitExprIdentifier(exprIdentifier);
                break;
            case ExprInt exprInt:
                VisitExprInt(exprInt);
                break;
            case ExprUnary exprUnary:
                VisitExprUnary(exprUnary);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expr));
        }
    }

    private void VisitExprBinary(ExprBinary expr)
    {
        VisitExpr(expr.Left);
        VisitExpr(expr.Right);

        Debug.Assert(expr.Left.ResolvedType != null);
        Debug.Assert(expr.Right.ResolvedType != null);

        Type leftType = expr.Left.ResolvedType;
        Type rightType = expr.Right.ResolvedType;

        if (leftType == BuiltinType.Error || rightType == BuiltinType.Error)
        {
            // Already reported
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        TokenType op = GetTokenType(expr.OperatorToken);
        Type? commonType = GetCommonType(leftType, rightType, op);
        if (commonType == null)
        {
            Error($"Cannot use \"{op}\" on \"{leftType}\" and \"{rightType}\"", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        expr.Left = Adapt(expr.Left, commonType);
        expr.Right = Adapt(expr.Right, commonType);
        expr.ResolvedType = commonType;
    }

    private void VisitExprCall(ExprCall expr)
    {
        VisitExpr(expr.Callee);
        foreach (Expr arg in expr.Args)
        {
            VisitExpr(arg);
        }

        if (expr.Callee is not ExprIdentifier callee)
        {
            // TODO: Implement more complex callees, like `myfunc()[4]()`
            Error("Only identifiers can be called", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        if (callee.Symbol == null)
        {
            // Already reported
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        if (callee.Symbol is not FuncSymbol funcSym)
        {
            Error($"Expected function, got \"{callee.Symbol.Name}\"({callee.Symbol.GetType().Name})", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        if (funcSym.Declaration.Params.Count != expr.Args.Count)
        {
            Error(
                $"Function \"{funcSym.Name}\" takes {funcSym.Declaration.Params.Count} arguments, got {expr.Args.Count}",
                expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        Debug.Assert(funcSym.Type is FuncType);
        FuncType funcType = (FuncType)funcSym.Type;

        for (int i = 0; i < expr.Args.Count; ++i)
        {
            Type paramType = funcType.ParamTypes[i];
            Expr arg = expr.Args[i];

            Debug.Assert(paramType != null, "Must be already resolved");
            Debug.Assert(arg.ResolvedType != null, "Must be resolved above");

            Expr newArg = Adapt(arg, paramType);
            expr.Args[i] = newArg;
        }

        expr.ResolvedType = funcType.ReturnType;
    }

    private void VisitExprIdentifier(ExprIdentifier expr)
    {
        ReadOnlySpan<char> name = GetTokenValue(expr.IdentifierToken);
        Symbol? sym = LookupRecursive(name);
        if (sym == null)
        {
            Error($"Symbol not found: \"{name}\"", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        switch (sym)
        {
            case FuncSymbol:
            case ParamSymbol:
            case VariableSymbol:
                break;
            case TypeSymbol:
                // TODO: Allow that, for e.g. `i32.TypeSize`
                Error($"Type cannot be used as an identifier: \"{name}\"", expr);
                expr.ResolvedType = BuiltinType.Error;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(sym));
        }

        expr.Symbol = sym;
        expr.ResolvedType = sym.Type;
    }

    private void VisitExprInt(ExprInt expr)
    {
        // TODO: Compute in parser/lexer? Duplicated with lexer
        ReadOnlySpan<char> str = GetTokenValue(expr.LiteralToken);

        try
        {
            expr.Value = ParseIntLiteralValue(str);
        }
        catch (Exception)
        {
            expr.Value = 0;
            Error($"Invalid integer literal: {str}", expr);
        }

        expr.ResolvedType = BuiltinType.I32;
    }

    private static ulong ParseIntLiteralValue(ReadOnlySpan<char> str)
    {
        // TODO: Can parse without ToString?
        if (str[0] != '0')
        {
            return Convert.ToUInt64(str.ToString(), 10);
        }

        if (str.Length == 1)
        {
            return 0;
        }

        char next = str[1];
        return next switch
        {
            'x' or 'X' => Convert.ToUInt64(str[2..].ToString(), 16),
            'b' or 'B' => Convert.ToUInt64(str[2..].ToString(), 2),
            _ => Convert.ToUInt64(str[1..].ToString(), 8)
        };
    }

    private void VisitExprUnary(ExprUnary expr)
    {
        VisitExpr(expr.Expr);
        Debug.Assert(expr.Expr.ResolvedType != null);

        TokenType op = GetTokenType(expr.OperatorToken);
        if (!CanUseUnary(expr.Expr.ResolvedType, op))
        {
            Error($"Cannot use unary operator \"{op}\" on type \"{expr.Expr.ResolvedType}\"", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        expr.ResolvedType = expr.Expr.ResolvedType;
    }

    private void RegisterBuiltin(Scope scope)
    {
        void Register(string name, Type type)
        {
            TypeSymbol symbol = new()
            {
                Name = name,
                DeclaringScope = scope,
                Type = type,
            };
            bool added = scope.TryDeclare(symbol);
            Debug.Assert(added);
        }

        // NOTE: Don't register void because it's not supposed to be used by user
        Register("i32", BuiltinType.I32);
    }

    private void RegisterFunctionSymbols(CompilationUnit unit)
    {
        foreach (FuncDecl fd in unit.FuncDecls)
        {
            AddFunctionSymbol(fd);
        }
    }

    private void AddFunctionSymbol(FuncDecl fd)
    {
        Type returnType = BuiltinType.Void;
        if (fd.ReturnType != null)
        {
            Type type = ResolveType(fd.ReturnType);
            returnType = type;
        }

        // TODO: Reuse list
        List<Type> paramTypes = [];
        foreach (Param param in fd.Params)
        {
            Type type = ResolveType(param.Type);
            paramTypes.Add(type);
        }

        Scope scope = CurrentScope();
        ReadOnlySpan<char> name = GetTokenValue(fd.NameToken);

        FuncType funcType = _typeRegistry.GetFuncType(returnType, paramTypes);
        FuncSymbol sym = new()
        {
            Declaration = fd,
            DeclaringScope = scope,
            Type = funcType,
            Name = name.ToString()
        };

        fd.Symbol = sym;

        // NOTE: Create symbol even if it's a redeclaration

        RegisterSymbol(sym);
    }

    private void RegisterSymbol(Symbol symbol)
    {
        // TODO: Lookup once

        Scope scope = symbol.DeclaringScope;

        string name = symbol.Name;
        Symbol? loc = scope.LookupLocal(name);
        if (loc != null)
        {
            ErrorRedeclaration(symbol, loc);
            return;
        }

        Symbol? rec = scope.LookupRecursive(name);
        if (rec != null)
        {
            switch (rec)
            {
                case ParamSymbol:
                case VariableSymbol:
                    WarningShadow(symbol, rec);
                    break;
                case FuncSymbol:
                case TypeSymbol:
                    // Only variables/params can be shadowed
                    ErrorRedeclaration(symbol, rec);
                    return;

                default: throw new Exception("Unknown symbol type: " + rec.GetType().Name);
            }
        }

        bool ok = scope.TryDeclare(symbol);
        Debug.Assert(ok);
    }

    private Type ResolveType(TypeDecl typeDecl)
    {
        Debug.Assert(typeDecl.ResolvedType == null);

        ReadOnlySpan<char> name = GetTokenValue(typeDecl.TypeNameToken);
        Symbol? sym = LookupRecursive(name);
        if (sym == null)
        {
            Error($"Type not found: \"{name}\"", typeDecl);
            typeDecl.ResolvedType = BuiltinType.Error;
            return typeDecl.ResolvedType;
        }

        TypeSymbol? typeSym = sym as TypeSymbol;
        if (typeSym == null)
        {
            Error($"Type expected: \"{name}\". Given: \"{sym.GetType().Name}\"", typeDecl);
            typeDecl.ResolvedType = BuiltinType.Error;
            return typeDecl.ResolvedType;
        }

        typeDecl.ResolvedType = typeSym.Type;
        return typeDecl.ResolvedType;
    }

    private Symbol? LookupLocal(ReadOnlySpan<char> name)
    {
        return CurrentScope().LookupLocal(name);
    }

    private Symbol? LookupRecursive(ReadOnlySpan<char> name)
    {
        return CurrentScope().LookupRecursive(name);
    }

    private void PushScope(Scope scope)
    {
        _scopes.Add(scope);
    }

    private void PopScope()
    {
        _scopes.RemoveAt(_scopes.Count - 1);
    }

    private Scope CurrentScope()
    {
        return _scopes[^1];
    }

    private Expr Adapt(Expr expr, Type targetType)
    {
        Debug.Assert(expr.ResolvedType != null, "Must be resolve before adapt");

        Type type = expr.ResolvedType;
        if (type == BuiltinType.Error || targetType == BuiltinType.Error)
        {
            // Already reported
            return expr;
        }

        if (type == targetType)
        {
            return expr;
        }

        if (CanImplicitlyCast(type, targetType))
        {
            Debug.Assert(expr.ResolvedType != targetType, "Don't need cast");
            ExprCast cast = new ExprImplicitCast
            {
                StartToken = expr.StartToken,
                EndToken = expr.EndToken,
                Operand = expr,
                Target = targetType,
            };
            return cast;
        }

        Error($"Cannot implicitly cast \"{type}\" to \"{targetType}\"", expr);
        return expr;
    }

    private Type? GetCommonType(Type a, Type b, TokenType op)
    {
        if (a == b)
        {
            return a;
        }

        // TODO: Consider op too

        if (CanImplicitlyCast(b, a))
        {
            return a;
        }

        if (CanImplicitlyCast(a, b))
        {
            return b;
        }

        return null;
    }

    private bool CanUseUnary(Type type, TokenType op)
    {
        // TODO: Put this info in type

        if (type == BuiltinType.I32)
        {
            return true;
        }

        return false;
    }

    private bool CanImplicitlyCast(Type from, Type to)
    {
        Debug.Assert(from != to);
        // TODO: Implement
        return false;
    }


    private ReadOnlySpan<char> GetTokenValue(int tokenIndex)
    {
        return _tokens[tokenIndex].Value(_code);
    }

    private TokenType GetTokenType(int tokenIndex)
    {
        return _tokens[tokenIndex].Type;
    }

    private void Error(string message, Node node)
    {
        _diag.AddError(message, _tokens[node.StartToken]);
    }

    private void ErrorRedeclaration(Symbol newSymbol, Symbol oldSymbol)
    {
        Debug.Assert(newSymbol.Name == oldSymbol.Name);
        Debug.Assert(newSymbol.DeclaringNode != null);

        string message = $"Redeclaration of {newSymbol.Name}";
        if (oldSymbol.DeclaringNode != null)
        {
            int oldSymbolToken = oldSymbol.DeclaringNode.StartToken;
            Token old = _tokens[oldSymbolToken];
            message += $". Previously declared at {old.Line}:{old.Column}";
        }

        int newSymbolToken = newSymbol.DeclaringNode.StartToken;
        _diag.AddError(message, _tokens[newSymbolToken]);
    }

    // TODO: Duplicated
    private void WarningShadow(Symbol newSymbol, Symbol oldSymbol)
    {
        Debug.Assert(newSymbol.Name == oldSymbol.Name);
        Debug.Assert(newSymbol.DeclaringNode != null);

        string message = $"Shadowing of {newSymbol.Name}";
        if (oldSymbol.DeclaringNode != null)
        {
            int oldSymbolToken = oldSymbol.DeclaringNode.StartToken;
            Token old = _tokens[oldSymbolToken];
            message += $". Previously declared at {old.Line}:{old.Column}";
        }

        int newSymbolToken = newSymbol.DeclaringNode.StartToken;
        _diag.AddWarning(message, _tokens[newSymbolToken]);
    }
}