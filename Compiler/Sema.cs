using System.Diagnostics;

namespace Compiler;

public class Sema
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly TypeRegistry _typeRegistry = new();
    private readonly List<Scope> _scopes = new(); // TODO: Do we need list? Or just current scope?

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

        PopScope();
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
                    PushScope(scope);
                    b.Scope = scope;
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
    }

    private void VisitStmtExpr(StmtExpr stmt)
    {
        VisitExpr(stmt.Expr);
    }

    private void VisitStmtLet(StmtLet stmt)
    {
        Debug.Assert(stmt.Expr != null || stmt.TypeDecl != null, "Must be guaranteed by parser");

        Type? type = null;
        if (stmt.Expr != null)
        {
            VisitExpr(stmt.Expr);
            type = stmt.Expr.ResolvedType;
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

        ReadOnlySpan<char> name = GetTokenValue(stmt.NameToken);
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
    }

    private void VisitExprCall(ExprCall expr)
    {
    }

    private void VisitExprIdentifier(ExprIdentifier expr)
    {
        ReadOnlySpan<char> name = GetTokenValue(expr.IdentifierToken);
        Symbol? sym = LookupRecursive(name);
        if (sym == null)
        {
            Error($"Identifier not found: {name}", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }
    }

    private void VisitExprInt(ExprInt expr)
    {
        expr.ResolvedType = BuiltinType.I32;
    }

    private void VisitExprUnary(ExprUnary expr)
    {
        VisitExpr(expr.Expr);
        Debug.Assert(expr.Expr.ResolvedType != null);

        TokenType op = GetTokenType(expr.OperatorToken);
        if (!CanUseUnary(expr.Expr.ResolvedType, op))
        {
            Error($"Cannot use unary operator {op} on {expr.Expr.ResolvedType}", expr);
            expr.Expr.ResolvedType = BuiltinType.Error;
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
            Error($"Type not found: {name}", typeDecl);
            typeDecl.ResolvedType = BuiltinType.Error;
            return typeDecl.ResolvedType;
        }

        TypeSymbol? typeSym = sym as TypeSymbol;
        if (typeSym == null)
        {
            Error($"Type expected: {name}. Given: {sym.GetType().Name}", typeDecl);
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

        Error($"Cannot implicitly cast {type} to {targetType}", expr);
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