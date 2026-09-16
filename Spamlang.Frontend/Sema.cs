using System.Diagnostics;

namespace Spamlang.Frontend;

public class Sema
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Scope> _scopes = new(); // TODO: Do we need list? Or just current scope?
    private readonly List<FuncSymbol> _funcStack = new();
    private readonly TypeRegistry _typeRegistry;

    // Optional, for LSP Server
    private Dictionary<int, Symbol>? _tokenToSymbol = null;

    public Sema(string code, IReadOnlyList<Token> tokens, Diagnostic diag, TypeRegistry typeRegistry)
    {
        _code = code;
        _diag = diag;
        _tokens = tokens;
        _typeRegistry = typeRegistry;
    }

    public void Run(CompilationUnit unit, Dictionary<int, Symbol>? outTokenToSymbol = null)
    {
        _tokenToSymbol = outTokenToSymbol;

        Scope scope = new(null);
        unit.Scope = scope;
        PushScope(scope);

        RegisterBuiltin(scope);

        RegisterFunctionSymbols(unit);

        VisitCompilationUnit(unit);

        CheckMain(unit);

        _tokenToSymbol = null;
    }

    private void CheckMain(CompilationUnit unit)
    {
        // TODO: Make it optional

        Debug.Assert(unit.Scope != null);

        Symbol? sym = unit.Scope.LookupLocal("main");
        if (sym == null)
        {
            Error("\"main\" function not found", unit);
            return;
        }

        Debug.Assert(sym.DeclaringNode != null);
        Node mainDecl = sym.DeclaringNode;

        FuncSymbol? mainSym = sym as FuncSymbol;
        if (mainSym == null)
        {
            Error($"\"main\" must be a function, got {sym.SymbolKindName}", mainDecl);
            return;
        }

        FuncType mainFunc = (FuncType)mainSym.Type;
        if (mainFunc.ReturnType != BuiltinType.I32)
        {
            // TODO: Allow void
            Error($"\"main\" must return i32, got {mainFunc.ReturnType}", mainDecl);
            return;
        }

        if (mainFunc.ParamTypes.Count > 0)
        {
            // TODO: Implement argc,argv
            Error($"\"main\" must have 0 params, got {mainFunc.ParamTypes.Count}", mainDecl);
            return;
        }
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
        Debug.Assert(fd.ReturnType == null || fd.ReturnType.ResolvedType != null, $"Must be resolved in {nameof(RegisterFunctionSymbols)}");

        _funcStack.Add(fd.Symbol);

        Scope scope = new(CurrentScope());
        PushScope(scope);

        foreach (Param param in fd.Params)
        {
            Debug.Assert(param.Type.ResolvedType != null, $"Must be resolved in {nameof(RegisterFunctionSymbols)}");

            SpamType type = param.Type.ResolvedType;
            ReadOnlySpan<char> name = GetTokenValue(param.NameToken);

            ParamSymbol sym = new()
            {
                Declaration = param,
                DeclaringScope = scope,
                Type = type,
                Name = name.ToString(),
            };

            param.Symbol = sym;
            RegisterSymbol(sym);

            if (_tokenToSymbol != null)
            {
                Debug.Assert(!_tokenToSymbol.ContainsKey(param.NameToken));
                _tokenToSymbol[param.NameToken] = sym;
            }
        }

        fd.Body.Scope = scope;
        VisitBlock(fd.Body, out Stmt? terminator);

        FuncType funcType = (FuncType)fd.Symbol.Type;
        if (funcType.ReturnType != BuiltinType.Void && terminator == null)
        {
            _diag.AddError($"No return statement on the end of function \"{fd.Symbol.Name}\"", _tokens[fd.EndToken]);
        }

        PopScope();

        Debug.Assert(_funcStack[^1] == fd.Symbol);
        _funcStack.RemoveAt(_funcStack.Count - 1);
    }

    private void VisitBlock(Block block, out Stmt? terminator)
    {
        Debug.Assert(block.Scope != null, "Block scope must be set from outside");

        terminator = null;
        bool unreachableReported = false;
        foreach (Stmt stmt in block.Stmts)
        {
            if (terminator != null && !unreachableReported)
            {
                unreachableReported = true;
                Token termTok = _tokens[terminator.StartToken];
                Warning($"Unreachable code, terminated at {termTok.Line}:{termTok.Column}", stmt);
            }

            switch (stmt)
            {
                case Block b:
                    Scope scope = new(CurrentScope());
                    b.Scope = scope;
                    PushScope(scope);
                    VisitBlock(b, out Stmt? innerTerminator);

                    if (innerTerminator != null)
                    {
                        terminator = innerTerminator;
                    }

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
                    terminator = stmtReturn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stmt));
            }
        }
    }

    private void VisitStmtAssign(StmtAssign stmt)
    {
        // TODO: Add assignable check (const), make functions lvalue

        VisitExpr(stmt.Target);
        VisitExpr(stmt.Value);

        Debug.Assert(stmt.Target.ResolvedType != null);
        Debug.Assert(stmt.Value.ResolvedType != null);

        if (stmt.Target.ResolvedType == BuiltinType.Error)
        {
            // Already reported
            return;
        }

        if (stmt.Target.ValueCategory != ValueCategory.LValue)
        {
            Error("Only lvalue can be used as assignment target", stmt.Target);
            return;
        }

        SpamType targetType = stmt.Target.ResolvedType;
        SpamType valueType = stmt.Value.ResolvedType;

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
        // NOTE: Uninit is ok, defaults to zero
        // NOTE: Variable of function type can be uninitialized too, but we don't care

        Debug.Assert(stmt.Expr != null || stmt.TypeDecl != null, "Must be guaranteed by parser");

        ReadOnlySpan<char> name = GetTokenValue(stmt.NameToken);

        SpamType? declType = null;
        if (stmt.TypeDecl != null)
        {
            declType = ResolveType(stmt.TypeDecl);
        }

        if (stmt.Expr != null)
        {
            VisitExpr(stmt.Expr);
            Debug.Assert(stmt.Expr.ResolvedType != null);
            SpamType exprType = stmt.Expr.ResolvedType;
            if (exprType == BuiltinType.Void)
            {
                string message = $"Cannot assign variable \"{name}\" to void";
                if (declType != null)
                {
                    message += $". Expected {declType}";
                }

                Error(message, stmt);
            }
            else if (declType != null)
            {
                stmt.Expr = Adapt(stmt.Expr, declType);
            }
            else
            {
                declType = exprType;
            }
        }

        if (declType == null)
        {
            // Already reported above
            declType = BuiltinType.Error;
        }

        VariableSymbol sym = new()
        {
            Declaration = stmt,
            Name = name.ToString(),
            DeclaringScope = CurrentScope(),
            Type = declType,
        };

        stmt.Symbol = sym;
        RegisterSymbol(sym);

        if (_tokenToSymbol != null)
        {
            Debug.Assert(!_tokenToSymbol.ContainsKey(stmt.NameToken));
            _tokenToSymbol[stmt.NameToken] = sym;
        }
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
        SpamType returnType = funcType.ReturnType;

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

        Debug.Assert(expr.ResolvedType != null);
        Debug.Assert(expr.ValueCategory != null);
    }

    private void VisitExprBinary(ExprBinary expr)
    {
        expr.ValueCategory = ValueCategory.RValue;

        VisitExpr(expr.Left);
        VisitExpr(expr.Right);

        Debug.Assert(expr.Left.ResolvedType != null);
        Debug.Assert(expr.Right.ResolvedType != null);

        SpamType leftType = expr.Left.ResolvedType;
        SpamType rightType = expr.Right.ResolvedType;

        if (leftType == BuiltinType.Error || rightType == BuiltinType.Error)
        {
            // Already reported
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        SpamType? commonType = GetBinaryResultType(leftType, rightType, expr.Op);
        if (commonType == null)
        {
            Error($"Cannot use \"{TokenUtils.ToString(expr.Op)}\" on \"{leftType}\" and \"{rightType}\"", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        expr.Left = Adapt(expr.Left, commonType);
        expr.Right = Adapt(expr.Right, commonType);
        expr.ResolvedType = commonType;
    }

    private void VisitExprCall(ExprCall expr)
    {
        // TODO: Support default parameters

        expr.ValueCategory = ValueCategory.RValue;

        VisitExpr(expr.Callee);
        foreach (ExprCallArg arg in expr.Args)
        {
            VisitExpr(arg.Expr);
        }

        if (expr.Callee.ResolvedType == BuiltinType.Error)
        {
            // Already reported
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        if (expr.Callee.ResolvedType is not FuncType funcType)
        {
            Error("Cannot call a non-function type", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        // Can be null if call is indirect (e.g. via variable or expr)
        FuncDecl? funcDecl = null;
        if (expr.Callee is ExprIdentifier callee)
        {
            Debug.Assert(callee.Symbol != null, "ResolvedType is OK, so symbol must be valid");

            // NOTE: Callee identifier is not always a FuncSymbol! E.g. variable with a pointer to function
            // Don't emit error for this!
            if (callee.Symbol is FuncSymbol funcSym)
            {
                funcDecl = funcSym.Declaration;
                Debug.Assert(funcDecl.Symbol == funcSym);
            }
        }

        IReadOnlyList<SpamType> funcParams = funcType.ParamTypes;
        List<ExprCallArg> args = expr.Args;
        bool[] usedParams = new bool[funcParams.Count];
        bool hasUnorderedNamedArgs = false;

        if (args.Count > funcParams.Count)
        {
            string str = "Function ";
            if (funcDecl != null)
            {
                str += $"\"{GetTokenValue(funcDecl.NameToken)}\" ";
            }

            str += $"accepts {funcParams.Count} arguments, got {args.Count}";
            Error(str, expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        for (int i = 0; i < args.Count; ++i)
        {
            ExprCallArg arg = args[i];
            if (hasUnorderedNamedArgs && arg.ArgNameToken == null)
            {
                Error("Cannot use positional arguments after named arguments in changed order", arg);
                expr.ResolvedType = BuiltinType.Error;
                return;
            }

            int paramIndex = i;
            if (arg.ArgNameToken != null)
            {
                if (funcDecl == null)
                {
                    Error("Cannot use named arguments with indirect calls", arg);
                    expr.ResolvedType = BuiltinType.Error;
                    return;
                }

                ReadOnlySpan<char> argName = GetTokenValue(arg.ArgNameToken.Value);
                paramIndex = FindParamIndexByName(funcDecl, argName);
                if (paramIndex == -1)
                {
                    Error($"Function \"{funcDecl.Symbol!.Name}\" doesn't have parameter with name \"{argName}\"", arg);
                    expr.ResolvedType = BuiltinType.Error;
                    return;
                }

                hasUnorderedNamedArgs |= paramIndex != i;
            }

            if (usedParams[paramIndex])
            {
                string err = $"Parameter {paramIndex + 1} ";
                if (funcDecl != null)
                {
                    err += $"({GetTokenValue(funcDecl.Params[paramIndex].NameToken)}) ";
                }

                err += "is specified twice";

                Error(err, arg);
                expr.ResolvedType = BuiltinType.Error;
                return;
            }

            usedParams[paramIndex] = true;

            SpamType paramType = funcParams[paramIndex];

            Debug.Assert(paramType != null, "Must be already resolved");
            Debug.Assert(arg.Expr.ResolvedType != null, "Must be resolved above");

            arg.ParameterIndex = paramIndex;
            arg.Expr = Adapt(arg.Expr, paramType);
        }

        for (int i = 0; i < usedParams.Length; i++)
        {
            bool parmUsed = usedParams[i];
            if (parmUsed)
            {
                continue;
            }

            string err = $"Parameter {i + 1} ";
            if (funcDecl != null)
            {
                err += $"({GetTokenValue(funcDecl.Params[i].NameToken)}) ";
            }

            err += "is missing";

            Error(err, expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        expr.ResolvedType = funcType.ReturnType;
    }

    private int FindParamIndexByName(FuncDecl funcDecl, ReadOnlySpan<char> name)
    {
        for (int i = 0; i < funcDecl.Params.Count; i++)
        {
            Param param = funcDecl.Params[i];
            ReadOnlySpan<char> paramName = GetTokenValue(param.NameToken);
            bool match = name.SequenceEqual(paramName);
            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private void VisitExprIdentifier(ExprIdentifier expr)
    {
        expr.ValueCategory = ValueCategory.RValue;

        ReadOnlySpan<char> name = GetTokenValue(expr.IdentifierToken);
        Symbol? sym = LookupRecursive(name);
        if (sym == null)
        {
            Error($"Symbol not found: \"{name}\"", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        if (_tokenToSymbol != null)
        {
            Debug.Assert(!_tokenToSymbol.ContainsKey(expr.IdentifierToken));
            _tokenToSymbol[expr.IdentifierToken] = sym;
        }

        switch (sym)
        {
            case ParamSymbol:
            case VariableSymbol:
                expr.ValueCategory = ValueCategory.LValue;
                break;
            case FuncSymbol:
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
        // TODO: Refactor, handle negation in lexer and make it a part of the literal?
        // TODO: Overflows checks

        expr.ValueCategory = ValueCategory.RValue;

        ReadOnlySpan<char> str = GetTokenValue(expr.LiteralToken);

        Int128 value = 0;
        try
        {
            value = ParseIntLiteralValue(str, expr.IsNegative);
        }
        catch (OverflowException)
        {
            ErrorOutOfRange(expr.IsNegative, str, expr);
        }
        catch (Exception)
        {
            Error($"Invalid integer literal: {str}", expr);
        }

        // TODO: Smart resolve type based on value
        if (value > Int32.MaxValue || value < Int32.MinValue)
        {
            ErrorOutOfRange(expr.IsNegative, str, expr);
        }

        expr.Value = value;
        expr.ResolvedType = BuiltinType.I32;
    }

    private static Int128 ParseIntLiteralValue(ReadOnlySpan<char> str, bool negative)
    {
        int radix = 10;
        if (str[0] == '0' && str.Length > 1)
        {
            switch (str[1])
            {
                case 'x' or 'X':
                    radix = 16;
                    str = str[2..];
                    break;
                case 'b' or 'B':
                    radix = 2;
                    str = str[2..];
                    break;
                default:
                    Debug.Assert(char.IsAsciiDigit(str[1]), "Must be guaranteed by lexer");
                    radix = 8;
                    str = str[1..];
                    break;
            }
        }

        Int128 value = 0;
        foreach (char c in str)
        {
            int digit;
            if (c >= 'a')
            {
                digit = c - 'a' + 10;
            }
            else if (c >= 'A')
            {
                digit = c - 'A' + 10;
            }
            else
            {
                digit = c - '0';
            }

            if (digit >= radix)
            {
                throw new FormatException();
            }

            checked
            {
                value = value * (Int128)radix + (Int128)digit;
            }
        }

        if (negative)
        {
            return checked(-value);
        }

        return value;
    }

    private void VisitExprUnary(ExprUnary expr)
    {
        expr.ValueCategory = ValueCategory.RValue;

        VisitExpr(expr.Expr);
        Debug.Assert(expr.Expr.ResolvedType != null);

        if (expr.Expr.ResolvedType == BuiltinType.Error)
        {
            // Already reported
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        if (!CanUseUnary(expr.Expr.ResolvedType, expr.Op))
        {
            Error($"Cannot use unary operator \"{expr.Op}\" on type \"{expr.Expr.ResolvedType}\"", expr);
            expr.ResolvedType = BuiltinType.Error;
            return;
        }

        expr.ResolvedType = expr.Expr.ResolvedType;
    }

    private void RegisterBuiltin(Scope scope)
    {
        void Register(string name, SpamType type)
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
        SpamType returnType = BuiltinType.Void;
        if (fd.ReturnType != null)
        {
            SpamType type = ResolveType(fd.ReturnType);
            returnType = type;
        }

        // TODO: Reuse list
        List<SpamType> paramTypes = [];
        foreach (Param param in fd.Params)
        {
            SpamType type = ResolveType(param.Type);
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
            Name = name.ToString(),
        };

        fd.Symbol = sym;

        // NOTE: Create symbol even if it's a redeclaration

        RegisterSymbol(sym);

        if (_tokenToSymbol != null)
        {
            Debug.Assert(!_tokenToSymbol.ContainsKey(fd.NameToken));
            _tokenToSymbol[fd.NameToken] = sym;
        }
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

                default: throw new Exception("Unknown symbol type: " + rec.SymbolKindName);
            }
        }

        bool ok = scope.TryDeclare(symbol);
        Debug.Assert(ok);
    }

    private SpamType ResolveType(TypeNode node)
    {
        Debug.Assert(node.ResolvedType == null);
        switch (node)
        {
            case FuncTypeNode n:
                return ResolveFuncType(n);
            case IdentifierTypeNode n:
                return ResolveIdentifierType(n);
            case PointerTypeNode n:
                return ResolvePointerType(n);
            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }

    private SpamType ResolveIdentifierType(IdentifierTypeNode node)
    {
        ReadOnlySpan<char> name = GetTokenValue(node.TypeNameToken);
        Symbol? sym = LookupRecursive(name);
        if (sym == null)
        {
            Error($"Type not found: \"{name}\"", node);
            node.ResolvedType = BuiltinType.Error;
            return node.ResolvedType;
        }

        if (_tokenToSymbol != null)
        {
            Debug.Assert(!_tokenToSymbol.ContainsKey(node.TypeNameToken));
            _tokenToSymbol[node.TypeNameToken] = sym;
        }

        TypeSymbol? typeSym = sym as TypeSymbol;
        if (typeSym == null)
        {
            Error($"Type expected: \"{name}\". Given: \"{sym.SymbolKindName}\"", node);
            node.ResolvedType = BuiltinType.Error;
            return node.ResolvedType;
        }

        node.ResolvedType = typeSym.Type;
        return node.ResolvedType;
    }

    private SpamType ResolveFuncType(FuncTypeNode node)
    {
        // TODO: Duplicated with AddFunctionSymbol 

        SpamType returnType = BuiltinType.Void;
        if (node.ReturnType != null)
        {
            SpamType type = ResolveType(node.ReturnType);
            returnType = type;
        }

        // TODO: Reuse list
        List<SpamType> paramTypes = [];
        foreach (TypeNode param in node.Params)
        {
            SpamType type = ResolveType(param);
            paramTypes.Add(type);
        }

        FuncType funcType = _typeRegistry.GetFuncType(returnType, paramTypes);
        node.ResolvedType = funcType;
        return funcType;
    }

    private SpamType ResolvePointerType(PointerTypeNode node)
    {
        // TODO: Support
        node.ResolvedType = BuiltinType.Error;
        Error("Pointer types are not supported yet", node);
        return node.ResolvedType;
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

    private Expr Adapt(Expr expr, SpamType targetType)
    {
        Debug.Assert(expr.ResolvedType != null, "Must be resolve before adapt");

        SpamType type = expr.ResolvedType;
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
                ResolvedType = targetType,
                ValueCategory = ValueCategory.RValue,
            };
            return cast;
        }

        Error($"Cannot implicitly cast \"{type}\" to \"{targetType}\"", expr);
        return expr;
    }

    private SpamType? GetBinaryResultType(SpamType a, SpamType b, BinaryOp op)
    {
        Debug.Assert(a != BuiltinType.Error && b != BuiltinType.Error);

        if (a != BuiltinType.I32 || b != BuiltinType.I32)
        {
            return null;
        }

        // TODO: Consider op too

        if (a == b)
        {
            return a;
        }

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

    private bool CanUseUnary(SpamType type, UnaryOp op)
    {
        // TODO: Put this info in type

        if (type == BuiltinType.I32)
        {
            return op == UnaryOp.Plus || op == UnaryOp.Minus;
        }

        return false;
    }

    private bool CanImplicitlyCast(SpamType from, SpamType to)
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

    private void Warning(string message, Node node)
    {
        _diag.AddWarning(message, _tokens[node.StartToken]);
    }

    private void ErrorOutOfRange(bool negative, ReadOnlySpan<char> str, Node node)
    {
        Error($"Integer literal out of range: {(negative ? "-" : "")}{str}", node);
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