using System.ComponentModel.DataAnnotations;
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

    public TypedCompilationUnit Run(CompilationUnit unit, Dictionary<int, Symbol>? outTokenToSymbol = null)
    {
        _tokenToSymbol = outTokenToSymbol;

        Scope scope = new(parent: null);
        PushScope(scope);

        RegisterBuiltinTypeSymbols();
        List<FuncSymbol> funcSymbols = RegisterFunctionSymbols(unit);
        TypedCompilationUnit compUnit = VisitCompilationUnit(unit, funcSymbols);
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

        Register("i32", BuiltinType.I32);
        Register("void", BuiltinType.Void);
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
            RegisterSymbol(sym);
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
        Symbol? sym = CurrentScope().LookupLocal("main");
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

        FuncType mainFunc = (FuncType)mainSym.Type;
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

    private TypedCompilationUnit VisitCompilationUnit(CompilationUnit unit, List<FuncSymbol> funcSymbols)
    {
        Debug.Assert(funcSymbols.Count == unit.FuncDecls.Count);

        List<TypedFuncDecl> functions = new();
        for (int i = 0; i < unit.FuncDecls.Count; i++)
        {
            FuncDecl fd = unit.FuncDecls[i];
            FuncSymbol funcSym = funcSymbols[i];
            TypedFuncDecl tfd = VisitFuncDecl(fd, funcSym);
            functions.Add(tfd);
        }

        return new TypedCompilationUnit
        {
            FuncDecls = functions,
            Syntax = unit,
            IsSynthesized = false,
        };
    }

    private TypedFuncDecl VisitFuncDecl(FuncDecl fd, FuncSymbol funcSym)
    {
        Scope scope = new(CurrentScope());
        PushFunc(funcSym);
        PushScope(scope);

        // NOTE: Params are already registered

        List<VariableSymbol> allVariables = new();
        TypedBlock body = VisitBlock(fd.Body, allVariables, out Stmt? terminator);

        if (funcSym.FuncType.ReturnType != BuiltinType.Void && terminator == null)
        {
            Error($"No return statement at the end of function \"{funcSym.Name}\"", fd.EndToken);
        }

        PopScope();
        PopFunc();

        return new TypedFuncDecl
        {
            Body = body,
            Symbol = funcSym,
            Variables = allVariables,
            Syntax = fd,
            IsSynthesized = false,
        };
    }

    private TypedBlock VisitBlock(Block block, List<VariableSymbol> allVariables, out Stmt? terminator)
    {
        terminator = null;
        bool unreachableReported = false;

        List<TypedStmt> stmts = new();
        List<VariableSymbol> variables = new();
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
                case Block stmtBlock:
                    Scope scope = new(CurrentScope());
                    PushScope(scope);

                    TypedBlock tb = VisitBlock(stmtBlock, allVariables, out Stmt? innerTerminator);
                    stmts.Add(tb);

                    PopScope();

                    if (innerTerminator != null)
                    {
                        terminator = innerTerminator;
                    }

                    break;
                case StmtAssign stmtAssign:
                    TypedStmtAssign tsa = VisitStmtAssign(stmtAssign);
                    stmts.Add(tsa);
                    break;
                case StmtExpr stmtExpr:
                    TypedStmtExpr tse = VisitStmtExpr(stmtExpr);
                    stmts.Add(tse);
                    break;
                case StmtLet stmtLet:
                    TypedStmtLet tsl = VisitStmtLet(stmtLet);
                    stmts.Add(tsl);
                    variables.Add(tsl.VariableSymbol);
                    break;
                case StmtReturn stmtReturn:
                    TypedStmtReturn tsr = VisitStmtReturn(stmtReturn);
                    stmts.Add(tsr);
                    terminator = stmtReturn;
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        allVariables.AddRange(variables);
        return new TypedBlock
        {
            Variables = variables,
            Stmts = stmts,
            Syntax = block,
            IsSynthesized = false,
        };
    }

    private TypedStmtAssign VisitStmtAssign(StmtAssign stmt)
    {
        TypedExpr target = VisitExpr(stmt.Target);
        TypedExpr value = VisitExpr(stmt.Value);

        if (target.Type != BuiltinType.Error && !target.IsLValue)
        {
            Error("Only lvalue can be used as assignment target", stmt.Target);
        }

        TypedExpr adaptedValue;
        if (target.Type == BuiltinType.Error || value.Type == BuiltinType.Error)
        {
            adaptedValue = value;
        }
        else
        {
            TypedExpr rvalue = ToRValue(value);
            adaptedValue = Adapt(rvalue, target.Type);
        }

        return new TypedStmtAssign
        {
            Target = target,
            Value = adaptedValue,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private TypedStmtExpr VisitStmtExpr(StmtExpr stmt)
    {
        TypedExpr expr = VisitExpr(stmt.Expr);
        return new TypedStmtExpr
        {
            Expr = expr,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private TypedStmtLet VisitStmtLet(StmtLet stmt)
    {
        // NOTE: Uninit is ok, defaults to zero
        // TODO: Variable of function type can be uninitialized, bad

        ReadOnlySpan<char> name = GetTokenValue(stmt.NameToken);

        SpamType? declType = null;
        if (stmt.TypeDecl != null)
        {
            declType = ResolveType(stmt.TypeDecl);
        }

        TypedExpr init;
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
            else if (declType != null && declType != BuiltinType.Error)
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
                declType = BuiltinType.Error;
            }

            init = new TypedZeroInit
            {
                Type = declType,
                Syntax = stmt,
                IsSynthesized = true,
            };
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

        return new TypedStmtLet
        {
            VariableSymbol = sym,
            Init = init,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private TypedStmtReturn VisitStmtReturn(StmtReturn stmt)
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

        TypedExpr? expr = null;
        if (stmt.Expr != null)
        {
            expr = VisitExpr(stmt.Expr);
            expr = ToRValue(expr);
            if (returnType != BuiltinType.Void)
            {
                expr = Adapt(expr, returnType);
            }
        }

        return new TypedStmtReturn
        {
            Value = expr,
            Syntax = stmt,
            IsSynthesized = false,
        };
    }

    private TypedExpr VisitExpr(Expr expr)
    {
        switch (expr)
        {
            case ExprBinary exprBinary:
                return VisitExprBinary(exprBinary);
            case ExprCall exprCall:
                return VisitExprCall(exprCall);
            case ExprIdentifier exprIdentifier:
                return VisitExprIdentifier(exprIdentifier);
            case ExprInt exprInt:
                return VisitExprInt(exprInt);
            case ExprUnary exprUnary:
                return VisitExprUnary(exprUnary);
            default:
                throw new ArgumentOutOfRangeException(nameof(expr));
        }
    }

    private TypedBinary VisitExprBinary(ExprBinary expr)
    {
        TypedExpr left = VisitExpr(expr.Left);
        TypedExpr right = VisitExpr(expr.Right);

        left = ToRValue(left);
        right = ToRValue(right);

        SpamType leftType = left.Type;
        SpamType rightType = right.Type;

        SpamType? commonType;
        if (leftType == BuiltinType.Error || rightType == BuiltinType.Error)
        {
            commonType = BuiltinType.Error;
        }
        else
        {
            commonType = GetBinaryResultType(leftType, rightType, expr.Op);
            if (commonType == null)
            {
                Error($"Cannot use \"{TokenUtils.ToString(expr.Op)}\" on \"{leftType}\" and \"{rightType}\"", expr);
                commonType = BuiltinType.Error;
            }
        }

        left = Adapt(left, commonType);
        right = Adapt(right, commonType);
        return new TypedBinary
        {
            Left = left,
            Right = right,
            Op = expr.Op,
            Type = commonType,
            Syntax = expr,
            IsSynthesized = false,
        };
    }

    private TypedCall VisitExprCall(ExprCall expr)
    {
        // TODO: Support default parameters

        TypedExpr callee = VisitExpr(expr.Callee);
        callee = ToRValue(callee);

        FuncType? funcType;
        if (callee.Type == BuiltinType.Error)
        {
            funcType = null;
        }
        else if (callee.Type is FuncType ft)
        {
            funcType = ft;
        }
        else
        {
            Error("Cannot call a non-function type", expr);
            funcType = null;
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

    private TypedExpr VisitExprIdentifier(ExprIdentifier expr)
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

        RegisterTokenAsSymbol(expr.IdentifierToken, sym);

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

    private TypedIntConst VisitExprInt(ExprInt expr)
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

    private TypedUnary VisitExprUnary(ExprUnary expr)
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

    private void RegisterSymbol(Symbol symbol)
    {
        // TODO: Lookup once

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
                case ParamSymbol:
                case VariableSymbol:
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
        _scopes.RemoveAt(_funcStack.Count - 1);
    }

    private FuncSymbol CurrentFunc()
    {
        return _funcStack[^1];
    }

    private TypedExpr Adapt(TypedExpr expr, SpamType targetType)
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

    private TypedExpr ToRValue(TypedExpr expr)
    {
        if (!expr.IsLValue || expr.Type == BuiltinType.Error)
        {
            return expr;
        }

        return new TypedLoad
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
}