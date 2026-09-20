using System.Diagnostics;

namespace Spamlang.Frontend;

public class Sema
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Scope> _scopes = new(); // TODO: Do we need list? Or just current scope?
    private readonly TypeRegistry _typeRegistry;

    // For local functions (declared inside functions)
    private readonly List<FuncSymbol> _funcStack = new();

    // Optional, for LSP Server
    private Dictionary<int, Symbol>? _tokenToSymbol = null;

    public Sema(string code, IReadOnlyList<Token> tokens, Diagnostic diag, TypeRegistry typeRegistry)
    {
        _code = code;
        _diag = diag;
        _tokens = tokens;
        _typeRegistry = typeRegistry;
    }

    public HIRCompilationUnit Run(CompilationUnit unit, Dictionary<int, Symbol>? outTokenToSymbol = null)
    {
        _tokenToSymbol = outTokenToSymbol;

        Scope scope = new(parent: null);
        PushScope(scope);

        RegisterBuiltinTypeSymbols();
        List<FuncSymbol> funcSymbols = RegisterFunctionSymbols(unit);
        HIRCompilationUnit compUnit = VisitCompilationUnit(unit, funcSymbols);
        CheckMain();

        PopScope();

        _tokenToSymbol = null;
        return compUnit;
    }

    private void RegisterBuiltinTypeSymbols()
    {
        void Register(string name, SpamType type)
        {
            TypeSymbol symbol = new()
            {
                Name = name,
                SymbolType = type,
            };
            bool added = CurrentScope().TryDeclare(symbol);
            Debug.Assert(added);
        }

        Register("i8", BuiltinType.I8);
        Register("i16", BuiltinType.I16);
        Register("i32", BuiltinType.I32);
        Register("i64", BuiltinType.I64);

        Register("u8", BuiltinType.U8);
        Register("u16", BuiltinType.U16);
        Register("u32", BuiltinType.U32);
        Register("u64", BuiltinType.U64);
    }

    private List<FuncSymbol> RegisterFunctionSymbols(CompilationUnit unit)
    {
        List<FuncSymbol> symbols = new();
        foreach (FuncDecl fd in unit.FuncDecls)
        {
            FuncSymbol sym = RegisterFunctionSymbol(fd);
            symbols.Add(sym);
        }

        return symbols;
    }

    private FuncSymbol RegisterFunctionSymbol(FuncDecl fd)
    {
        SpamType returnType = BuiltinType.Void;
        if (fd.ReturnType != null)
        {
            SpamType type = ResolveType(fd.ReturnType);
            returnType = type;
        }

        // TODO: Reuse list
        List<SpamType> paramTypes = new();
        List<ParamSymbol> paramSymbols = new();
        foreach (Param param in fd.Params)
        {
            SpamType type = ResolveType(param.Type);
            ParamSymbol sym = new()
            {
                Declaration = param,
                Name = GetTokenValue(param.NameToken).ToString(),
                ParamType = type,
            };
            // NOTE: Don't register param symbol right now, do this in function scope!  
            // RegisterSymbol(sym);
            RegisterTokenAsSymbol(param.NameToken, sym);
            paramTypes.Add(type);
            paramSymbols.Add(sym);
        }

        FuncType funcType = _typeRegistry.GetFuncType(returnType, paramTypes);
        FuncSymbol funcSym = new()
        {
            Declaration = fd,
            FuncType = funcType,
            Name = GetTokenValue(fd.NameToken).ToString(),
            Params = paramSymbols,
        };

        RegisterSymbol(funcSym);
        RegisterTokenAsSymbol(fd.NameToken, funcSym);

        return funcSym;
    }

    private void CheckMain()
    {
        // TODO: Make it optional

        string main = "main";
        Symbol? sym = CurrentScope().LookupLocal(main);
        if (sym == null)
        {
            Error($"\"{main}\" function not found");
            return;
        }

        Debug.Assert(sym.DeclaringNode != null);
        Node mainDecl = sym.DeclaringNode;

        FuncSymbol? mainSym = sym as FuncSymbol;
        if (mainSym == null)
        {
            Error($"\"{main}\" must be a function, got {sym.SymbolKindName}", mainDecl);
            return;
        }

        FuncType mainFunc = mainSym.FuncType;
        if (mainFunc.ReturnType != BuiltinType.I32)
        {
            // TODO: Allow void
            Error($"\"{main}\" must return i32, got {mainFunc.ReturnType}", mainDecl);
            return;
        }

        if (mainFunc.ParamTypes.Count > 0)
        {
            // TODO: Implement argc,argv
            Error($"\"{main}\" must have 0 params, got {mainFunc.ParamTypes.Count}", mainDecl);
            return;
        }
    }

    private HIRCompilationUnit VisitCompilationUnit(CompilationUnit unit, List<FuncSymbol> funcSymbols)
    {
        Debug.Assert(funcSymbols.Count == unit.FuncDecls.Count);

        List<HIRFuncDecl> functions = new();
        for (int i = 0; i < unit.FuncDecls.Count; i++)
        {
            FuncDecl fd = unit.FuncDecls[i];
            FuncSymbol funcSym = funcSymbols[i];
            HIRFuncDecl tfd = VisitFuncDecl(fd, funcSym);
            functions.Add(tfd);
        }

        return new HIRCompilationUnit
        {
            FuncDecls = functions,
            Syntax = unit,
            IsSynthesized = false,
        };
    }

    private HIRFuncDecl VisitFuncDecl(FuncDecl fd, FuncSymbol funcSym)
    {
        Scope scope = new(CurrentScope());
        PushFunc(funcSym);
        PushScope(scope);

        foreach (ParamSymbol paramSymbol in funcSym.Params)
        {
            RegisterSymbol(paramSymbol);
        }

        List<VariableSymbol> allVariables = new();
        HIRBlock body = VisitBlock(fd.Body, allVariables, out Stmt? terminator);

        if (funcSym.FuncType.ReturnType != BuiltinType.Void && terminator == null)
        {
            Error($"No return statement at the end of function \"{funcSym.Name}\"", fd.EndToken);
        }

        PopScope();
        PopFunc();

        return new HIRFuncDecl
        {
            Body = body,
            Symbol = funcSym,
            Locals = allVariables,
            Syntax = fd,
            IsSynthesized = false,
        };
    }

    private HIRBlock VisitBlock(Block block, List<VariableSymbol> allVariables, out Stmt? firstTerminator)
    {
        Stmt? firstTerm = null;
        List<HIRStmt> stmts = new();
        List<VariableSymbol> variables = new();

        bool unreachableReported = false;

        void AddStatement(HIRStmt stmt)
        {
            if (firstTerm == null)
            {
                stmts.Add(stmt);
                return;
            }

            if (unreachableReported)
            {
                return;
            }

            unreachableReported = true;
            Token termTok = _tokens[firstTerm.StartToken];
            Warning($"Unreachable code, terminated at {termTok.Line}:{termTok.Column}", stmt.Syntax);
        }

        foreach (Stmt stmt in block.Stmts)
        {
            switch (stmt)
            {
                case Block stmtBlock:
                    Scope scope = new(CurrentScope());
                    PushScope(scope);

                    HIRBlock tb = VisitBlock(stmtBlock, allVariables, out Stmt? innerTerm);
                    AddStatement(tb);

                    PopScope();

                    if (innerTerm != null && firstTerm == null)
                    {
                        firstTerm = innerTerm;
                    }

                    break;
                case StmtAssign stmtAssign:
                    HIRStmtAssign tsa = VisitStmtAssign(stmtAssign);
                    AddStatement(tsa);
                    break;
                case StmtExpr stmtExpr:
                    HIRStmtExpr tse = VisitStmtExpr(stmtExpr);
                    AddStatement(tse);
                    break;
                case StmtLet stmtLet:
                    HIRStmtLet tsl = VisitStmtLet(stmtLet);
                    AddStatement(tsl);
                    variables.Add(tsl.VariableSymbol);
                    break;
                case StmtReturn stmtReturn:
                    HIRStmtReturn tsr = VisitStmtReturn(stmtReturn);
                    AddStatement(tsr);
                    if (firstTerm == null)
                    {
                        firstTerm = stmtReturn;
                    }

                    break;
                default:
                    throw new UnreachableException();
            }
        }

        allVariables.AddRange(variables);
        firstTerminator = firstTerm;
        return new HIRBlock
        {
            Variables = variables,
            Stmts = stmts,
            Syntax = block,
            IsSynthesized = false,
        };
    }

    private HIRStmtAssign VisitStmtAssign(StmtAssign stmt)
    {
        HIRExpr target = VisitExpr(stmt.Target);
        if (target.Type != BuiltinType.Error && !target.IsLValue)
        {
            Error("Only lvalue can be used as assignment target", stmt.Target);
        }

        HIRExpr value = VisitExpr(stmt.Value);
        value = ToRValue(value);
        value = Adapt(value, target.Type);

        return new HIRStmtAssign
        {
            Target = target,
            Value = value,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private HIRStmtExpr VisitStmtExpr(StmtExpr stmt)
    {
        HIRExpr expr = VisitExpr(stmt.Expr);
        expr = ToRValue(expr);
        return new HIRStmtExpr
        {
            Expr = expr,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private HIRStmtLet VisitStmtLet(StmtLet stmt)
    {
        // NOTE: Uninit is ok, defaults to zero
        // TODO: Variable of function type can be uninitialized, bad

        ReadOnlySpan<char> name = GetTokenValue(stmt.NameToken);

        SpamType? declType = null;
        if (stmt.TypeDecl != null)
        {
            declType = ResolveType(stmt.TypeDecl);
        }

        HIRExpr init;
        if (stmt.Expr != null)
        {
            init = VisitExpr(stmt.Expr);
            init = ToRValue(init);
            if (init.Type == BuiltinType.Void)
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
                init = Adapt(init, declType);
            }
            else
            {
                declType = init.Type;
            }
        }
        else
        {
            if (declType == null)
            {
                // Either type or default value must be specified
                declType = BuiltinType.Error;
                init = new HIRExprError
                {
                    Children = [],
                    Type = BuiltinType.Error,
                    Syntax = stmt,
                    IsSynthesized = true,
                };
            }
            else if (declType is FuncType)
            {
                Error("Cannot leave variable with function type not initialized", stmt);
                init = new HIRExprError
                {
                    Children = [],
                    Type = BuiltinType.Error,
                    Syntax = stmt,
                    IsSynthesized = true,
                };
            }
            else
            {
                init = new HIRExprZeroInit
                {
                    Type = declType,
                    Syntax = stmt,
                    IsSynthesized = true,
                };
            }
        }

        if (declType == null)
        {
            // Already reported
            declType = BuiltinType.Error;
        }

        VariableSymbol sym = new()
        {
            Declaration = stmt,
            Name = name.ToString(),
            VariableType = declType,
        };

        RegisterSymbol(sym);
        RegisterTokenAsSymbol(stmt.NameToken, sym);

        return new HIRStmtLet
        {
            VariableSymbol = sym,
            Init = init,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private HIRStmtReturn VisitStmtReturn(StmtReturn stmt)
    {
        FuncSymbol currentFunc = CurrentFunc();
        FuncType funcType = currentFunc.FuncType;
        SpamType returnType = funcType.ReturnType;

        if (returnType == BuiltinType.Void && stmt.Expr != null)
        {
            Error($"Unexpected expression in return statement. Function \"{currentFunc.Name}\" returns void", stmt);
        }

        if (returnType != BuiltinType.Void && stmt.Expr == null)
        {
            Error($"Function \"{currentFunc.Name}\" must return value", stmt);
        }

        HIRExpr? expr = null;
        if (stmt.Expr != null)
        {
            expr = VisitExpr(stmt.Expr);
            expr = ToRValue(expr);
            if (returnType != BuiltinType.Void)
            {
                expr = Adapt(expr, returnType);
            }
        }

        return new HIRStmtReturn
        {
            Value = expr,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private HIRExpr VisitExpr(Expr expr)
    {
        switch (expr)
        {
            case ExprBinary exprBinary:
                return VisitExprBinary(exprBinary);
            case ExprCall exprCall:
                return VisitExprCall(exprCall);
            case ExprCast exprCast:
                return VisitExprCast(exprCast);
            case ExprIdentifier exprIdentifier:
                return VisitExprIdentifier(exprIdentifier);
            case ExprIntConst exprInt:
                return VisitExprIntConst(exprInt);
            case ExprUnary exprUnary:
                return VisitExprUnary(exprUnary);
            default:
                throw new ArgumentOutOfRangeException(nameof(expr));
        }
    }

    private HIRExprBinary VisitExprBinary(ExprBinary expr)
    {
        HIRExpr left = VisitExpr(expr.Left);
        HIRExpr right = VisitExpr(expr.Right);

        left = ToRValue(left);
        right = ToRValue(right);

        SpamType leftType = left.Type;
        SpamType rightType = right.Type;

        SpamType? resultType;
        if (leftType == BuiltinType.Error || rightType == BuiltinType.Error)
        {
            resultType = BuiltinType.Error;
        }
        else
        {
            resultType = GetBinaryResultType(leftType, rightType, expr.Op);
            if (resultType == null)
            {
                Error($"Cannot use \"{TokenUtils.ToString(expr.Op)}\" on \"{leftType}\" and \"{rightType}\"", expr);
                resultType = BuiltinType.Error;
            }
        }

        left = Adapt(left, resultType);
        right = Adapt(right, resultType);
        return new HIRExprBinary
        {
            Left = left,
            Right = right,
            Op = expr.Op,
            Type = resultType,
            Syntax = expr,
            IsSynthesized = false,
        };
    }

    private HIRExpr VisitExprCall(ExprCall expr)
    {
        // TODO: Support default parameters

        List<HIRCallArg> hirArgs = new();

        HIRExpr callee = VisitExpr(expr.Callee);
        callee = ToRValue(callee);

        if (callee.Type == BuiltinType.Error)
        {
            return ErrorCall(expr, callee, hirArgs);
        }

        if (callee.Type is not FuncType funcType)
        {
            Error("Cannot call a non-function type", expr);
            return ErrorCall(expr, callee, hirArgs);
        }

        // Can be null if the call is indirect (via variable or expr)
        FuncSymbol? funcSymbol = null;
        if (callee is HIRExprFuncRef funcRef)
        {
            funcSymbol = funcRef.Symbol;
            Debug.Assert(funcSymbol.Params.Count == funcType.ParamTypes.Count);
        }

        IReadOnlyList<SpamType> funcParams = funcType.ParamTypes;
        IReadOnlyList<CallArg> args = expr.Args;
        if (args.Count > funcParams.Count)
        {
            string str = "Function ";
            if (funcSymbol != null)
            {
                str += $"\"{funcSymbol.Name}\" ";
            }

            str += $"accepts {funcParams.Count} arguments, got {args.Count}";
            Error(str, expr);
            return ErrorCall(expr, callee, hirArgs);
        }

        bool[] usedParams = new bool[funcParams.Count];
        bool hasUnorderedNamedArgs = false;
        for (int i = 0; i < args.Count; ++i)
        {
            CallArg arg = args[i];
            if (hasUnorderedNamedArgs && arg.ArgNameToken == null)
            {
                Error("Cannot use positional arguments after named arguments in changed order", arg);
                return ErrorCall(expr, callee, hirArgs);
            }

            int paramIndex = i;
            if (arg.ArgNameToken != null)
            {
                if (funcSymbol == null)
                {
                    Error("Cannot use named arguments with indirect calls", arg);
                    return ErrorCall(expr, callee, hirArgs);
                }

                ReadOnlySpan<char> argName = GetTokenValue(arg.ArgNameToken.Value);
                paramIndex = FindParamIndexByName(funcSymbol, argName);
                if (paramIndex == -1)
                {
                    Error($"Function \"{funcSymbol.Name}\" doesn't have parameter with name \"{argName}\"", arg);
                    return ErrorCall(expr, callee, hirArgs);
                }

                hasUnorderedNamedArgs |= paramIndex != i;
            }

            if (usedParams[paramIndex])
            {
                string err = $"Parameter {paramIndex + 1} ";
                if (funcSymbol != null)
                {
                    err += $"({funcSymbol.Params[paramIndex].Name}) ";
                }

                err += "is already specified";

                Error(err, arg);
                return ErrorCall(expr, callee, hirArgs);
            }

            usedParams[paramIndex] = true;

            SpamType paramType = funcParams[paramIndex];
            HIRExpr argExpr = VisitExpr(arg.Value);
            argExpr = ToRValue(argExpr);
            argExpr = Adapt(argExpr, paramType);

            HIRCallArg hirCallArg = new()
            {
                Value = argExpr,
                ParameterIndex = paramIndex,
                Syntax = arg,
                IsSynthesized = false,
            };
            hirArgs.Add(hirCallArg);
        }

        for (int i = 0; i < usedParams.Length; i++)
        {
            bool paramUsed = usedParams[i];
            if (paramUsed)
            {
                continue;
            }

            string err = $"Parameter {i + 1} ";
            if (funcSymbol != null)
            {
                err += $"({funcSymbol.Params[i].Name}) ";
            }

            err += "is missing";

            Error(err, expr);
            return ErrorCall(expr, callee, hirArgs);
        }

        return new HIRExprCall
        {
            Callee = callee,
            Args = hirArgs,
            Type = funcType.ReturnType,
            Syntax = expr,
            IsSynthesized = false,
        };
    }

    private HIRExprError ErrorCall(ExprCall expr, HIRExpr callee, List<HIRCallArg> visitedArgs)
    {
        List<HIRExpr> children = new();
        children.EnsureCapacity(1 + expr.Args.Count);
        children.Add(callee);

        foreach (HIRCallArg arg in visitedArgs)
        {
            children.Add(arg.Value);
        }

        // Visit remaining args to emit errors for them too
        for (int i = visitedArgs.Count; i < expr.Args.Count; i++)
        {
            CallArg arg = expr.Args[i];
            HIRExpr hirArg = VisitExpr(arg.Value);
            hirArg = ToRValue(hirArg);
            children.Add(hirArg);
        }

        Debug.Assert(children.Count == expr.Args.Count + 1);
        return new HIRExprError
        {
            Children = children,
            Type = BuiltinType.Error,
            Syntax = expr,
            IsSynthesized = false,
        };
    }

    private HIRExpr VisitExprCast(ExprCast expr)
    {
        HIRExpr value = VisitExpr(expr.Value);
        SpamType targetType = ResolveType(expr.TargetType);
        if (value.Type == BuiltinType.Error || targetType == BuiltinType.Error)
        {
            // Already reported
        }
        else if (value.Type == targetType)
        {
            return value;
        }
        else if (!CanExplicitlyCast(value.Type, targetType))
        {
            Error($"Cannot cast \"{value.Type}\" to \"{targetType}\"", expr.TargetType);
            return new HIRExprError
            {
                Children = [value],
                Type = BuiltinType.Error,
                Syntax = expr,
                IsSynthesized = true,
            };
        }

        return new HIRExprCast
        {
            Value = value,
            Type = targetType,
            Syntax = expr,
            IsSynthesized = false,
        };
    }

    private HIRExpr VisitExprIdentifier(ExprIdentifier expr)
    {
        ReadOnlySpan<char> name = GetTokenValue(expr.IdentifierToken);
        Symbol? sym = LookupRecursive(name);
        if (sym == null)
        {
            Error($"Symbol not found: \"{name}\"", expr);
            return new HIRExprError
            {
                Children = [],
                Type = BuiltinType.Error,
                Syntax = expr,
                IsSynthesized = false,
            };
        }

        RegisterTokenAsSymbol(expr.IdentifierToken, sym);

        switch (sym)
        {
            case LocalSymbol localSym:
                return new HIRExprLocalRef
                {
                    Symbol = localSym,
                    Type = localSym.Type,
                    Syntax = expr,
                    IsSynthesized = false,
                };
            case FuncSymbol funcSym:
                return new HIRExprFuncRef
                {
                    Symbol = funcSym,
                    Type = funcSym.FuncType,
                    Syntax = expr,
                    IsSynthesized = false,
                };
            case TypeSymbol:
                // TODO: Allow that, for e.g. `i32.TypeSize()`
                Error($"Type cannot be used as an identifier: \"{name}\"", expr);
                return new HIRExprError
                {
                    Children = [],
                    Type = BuiltinType.Error,
                    Syntax = expr,
                    IsSynthesized = false,
                };
            default:
                throw new UnreachableException();
        }
    }

    private HIRExprIntConst VisitExprIntConst(ExprIntConst expr)
    {
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

        return new HIRExprIntConst
        {
            Value = value,
            Type = BuiltinType.AbstractNumber,
            Syntax = expr,
            IsSynthesized = false,
        };
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

    private HIRExprUnary VisitExprUnary(ExprUnary expr)
    {
        HIRExpr operand = VisitExpr(expr.Operand);
        operand = ToRValue(operand);

        SpamType type;
        if (operand.Type == BuiltinType.Error)
        {
            type = BuiltinType.Error;
        }
        else if (!CanUseUnary(operand.Type, expr.Op))
        {
            Error($"Cannot use unary operator \"{expr.Op}\" on type \"{operand.Type}\"", expr);
            type = BuiltinType.Error;
        }
        else
        {
            type = operand.Type;
        }

        return new HIRExprUnary
        {
            Op = expr.Op,
            Operand = operand,
            Type = type,
            Syntax = expr,
            IsSynthesized = false,
        };
    }

    private void RegisterSymbol(Symbol symbol)
    {
        Scope scope = CurrentScope();

        string name = symbol.Name;
        Symbol? existing = scope.LookupAny(name, out bool isLocal);
        if (existing != null)
        {
            if (isLocal)
            {
                ErrorRedeclaration(symbol, existing);
                return;
            }

            switch (existing)
            {
                case LocalSymbol:
                    WarningShadow(symbol, existing);
                    break;
                case FuncSymbol:
                case TypeSymbol:
                    // Only variables/params can be shadowed
                    ErrorRedeclaration(symbol, existing);
                    return;
                default: throw new UnreachableException();
            }
        }

        bool ok = scope.TryDeclare(symbol);
        Debug.Assert(ok);
    }

    private void RegisterTokenAsSymbol(int tokenIndex, Symbol symbol)
    {
        if (_tokenToSymbol == null)
        {
            return;
        }

        Debug.Assert(!_tokenToSymbol.ContainsKey(tokenIndex));
        _tokenToSymbol[tokenIndex] = symbol;
    }

    private SpamType ResolveType(TypeNode node)
    {
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
            return BuiltinType.Error;
        }

        RegisterTokenAsSymbol(node.TypeNameToken, sym);

        TypeSymbol? typeSym = sym as TypeSymbol;
        if (typeSym == null)
        {
            Error($"Type expected: \"{name}\". Given: \"{sym.SymbolKindName}\"", node);
            return BuiltinType.Error;
        }

        return typeSym.Type;
    }

    private SpamType ResolveFuncType(FuncTypeNode node)
    {
        SpamType returnType = BuiltinType.Void;
        if (node.ReturnType != null)
        {
            SpamType type = ResolveType(node.ReturnType);
            returnType = type;
        }

        // TODO: Reuse list
        List<SpamType> paramTypes = new();
        foreach (TypeNode param in node.Params)
        {
            SpamType type = ResolveType(param);
            paramTypes.Add(type);
        }

        FuncType funcType = _typeRegistry.GetFuncType(returnType, paramTypes);
        return funcType;
    }

    private SpamType ResolvePointerType(PointerTypeNode node)
    {
        // TODO: Support
        Error("Pointer types are not supported yet", node);
        return BuiltinType.Error;
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

    private void PushFunc(FuncSymbol func)
    {
        _funcStack.Add(func);
    }

    private void PopFunc()
    {
        _funcStack.RemoveAt(_funcStack.Count - 1);
    }

    private FuncSymbol CurrentFunc()
    {
        return _funcStack[^1];
    }

    private HIRExpr Adapt(HIRExpr expr, SpamType targetType)
    {
        SpamType type = expr.Type;
        if (type == BuiltinType.Error || targetType == BuiltinType.Error)
        {
            // Already reported
            return expr;
        }

        if (type == targetType)
        {
            return expr;
        }

        if (!CanImplicitlyCast(type, targetType))
        {
            Error($"Cannot implicitly cast \"{type}\" to \"{targetType}\"", expr.Syntax);
            return new HIRExprError
            {
                Children = [expr],
                Type = BuiltinType.Error,
                Syntax = expr.Syntax,
                IsSynthesized = true,
            };
        }

        return new HIRExprCast
        {
            Value = expr,
            Type = targetType,
            Syntax = expr.Syntax,
            IsSynthesized = true,
        };
    }

    private HIRExpr ToRValue(HIRExpr expr)
    {
        if (!expr.IsLValue || expr.Type == BuiltinType.Error)
        {
            return expr;
        }

        return new HIRExprLoad
        {
            Address = expr,
            Type = expr.Type,
            Syntax = expr.Syntax,
            IsSynthesized = true,
        };
    }

    private SpamType? GetBinaryResultType(SpamType a, SpamType b, BinaryOp op)
    {
        Debug.Assert(a != BuiltinType.Error && b != BuiltinType.Error);

        if (!a.IsInteger() || !b.IsInteger())
        {
            return null;
        }

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
        Debug.Assert(from != to, "Must be different types!");

        if (to.Kind == TypeKind.AbstractNumber)
        {
            Debug.Assert(from.Kind != TypeKind.AbstractNumber);
            return false;
        }

        if (to.Kind == TypeKind.SignedInteger)
        {
            if (from.Kind == TypeKind.AbstractNumber)
            {
                return true;
            }

            if (from.Kind == TypeKind.SignedInteger)
            {
                return to.Size >= from.Size;
            }

            if (from.Kind == TypeKind.UnsignedInteger)
            {
                return to.Size > from.Size;
            }

            return false;
        }

        if (to.Kind == TypeKind.UnsignedInteger)
        {
            if (from.Kind == TypeKind.AbstractNumber)
            {
                return true;
            }

            if (from.Kind == TypeKind.UnsignedInteger)
            {
                return to.Size >= from.Size;
            }

            return false;
        }

        if (to.Kind == TypeKind.Float)
        {
            if (from.Kind == TypeKind.AbstractNumber)
            {
                return true;
            }

            if (from.Kind == TypeKind.Float)
            {
                return to.Size >= from.Size;
            }

            if (from.Kind == TypeKind.SignedInteger || from.Kind == TypeKind.UnsignedInteger)
            {
                Debug.Assert(to.Size == 4 || to.Size == 8, "Condition below works only for f32/f64");
                return to.Size > from.Size;
            }

            return false;
        }

        return false;
    }

    private bool CanExplicitlyCast(SpamType from, SpamType to)
    {
        Debug.Assert(from != to);
        // TODO: Implement
        return false;
    }

    private ReadOnlySpan<char> GetTokenValue(int tokenIndex)
    {
        return _tokens[tokenIndex].Value(_code);
    }

    private void Error(string message)
    {
        _diag.AddError(message, _tokens[^1]);
    }

    private void Error(string message, int tokenIndex)
    {
        _diag.AddError(message, _tokens[tokenIndex]);
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

    private static int FindParamIndexByName(FuncSymbol funcSymbol, ReadOnlySpan<char> name)
    {
        for (int i = 0; i < funcSymbol.Params.Count; i++)
        {
            ParamSymbol param = funcSymbol.Params[i];
            bool match = name.SequenceEqual(param.Name);
            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}