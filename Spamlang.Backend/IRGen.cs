using System.Diagnostics;
using Spamlang.Frontend;

namespace Spamlang.Backend;

public class IRGen
{
    private readonly List<Dictionary<Symbol, IRValue>> _symbolScopes = new();
    private readonly IRTypeLowerer _lowerer;

    public IRGen(IRTypeLowerer lowerer)
    {
        _lowerer = lowerer;
    }

    public IRModule Run(HIRCompilationUnit unit)
    {
        Dictionary<Symbol, IRValue> globalScope = new();
        PushSymScope(globalScope);

        List<IRFunction> functions = new();
        foreach (HIRFuncDecl funcDecl in unit.FuncDecls)
        {
            FuncType signature = funcDecl.Symbol.FuncType;
            IRSignature lowerSignature = _lowerer.ToLowerSignature(signature);
            IRFunction func = new()
            {
                BasicBlocks = new List<IRBasicBlock>(),
                Name = funcDecl.Symbol.Name,
                Params = new List<IRParam>(),
                LoweredSignature = lowerSignature,
            };
            globalScope.Add(funcDecl.Symbol, func);
            functions.Add(func);
        }

        for (int i = 0; i < unit.FuncDecls.Count; i++)
        {
            HIRFuncDecl funcDecl = unit.FuncDecls[i];
            IRFunction function = functions[i];
            GenFunction(funcDecl, function);
        }

        IRModule module = new()
        {
            Functions = functions,
        };

        PopSymScope();

        AllocateIds(module);

        return module;
    }

    private void GenFunction(HIRFuncDecl funcDecl, IRFunction function)
    {
        IRBasicBlock entry = new()
        {
            Instructions = new List<IRInstruction>(),
            Name = "entry",
        };
        function.BasicBlocks.Add(entry);

        Dictionary<Symbol, IRValue> funcScope = new();
        PushSymScope(funcScope);

        GenParams(entry, funcDecl.Symbol.Params, function.Params);
        GenLocals(entry, funcDecl.Locals);

        GenStmts(entry, funcDecl.Body.Stmts);

        if (function.LoweredSignature.ReturnType == IRType.Void && entry.Terminator == null)
        {
            IRInstructionRet ret = new()
            {
                Value = null,
            };
            entry.Add(ret);
        }

        if (entry.Terminator == null)
        {
            throw new InvalidHIRException("Non void function must have a return statement at the end");
        }

        PopSymScope();
    }

    private void GenStmts(IRBasicBlock block, IReadOnlyList<HIRStmt> stmts)
    {
        foreach (HIRStmt stmt in stmts)
        {
            switch (stmt)
            {
                case HIRBlock b:
                    GenStmts(block, b.Stmts);
                    break;
                case HIRStmtAssign stmtAssign:
                    GenStmtAssign(block, stmtAssign);
                    break;
                case HIRStmtExpr stmtExpr:
                    GenStmtExpr(block, stmtExpr);
                    break;
                case HIRStmtLet stmtLet:
                    GenStmtLet(block, stmtLet);
                    break;
                case HIRStmtReturn stmtReturn:
                    GenStmtReturn(block, stmtReturn);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
    }

    private void GenStmtAssign(IRBasicBlock block, HIRStmtAssign stmtAssign)
    {
        IRValue value = GenExprValue(block, stmtAssign.Value);
        IRValue addr = GenExprAddr(block, stmtAssign.Target);
        IRInstructionStore store = new()
        {
            Value = value,
            Address = addr,
        };
        block.Add(store);
    }

    private void GenStmtExpr(IRBasicBlock block, HIRStmtExpr stmtExpr)
    {
        GenExprValue(block, stmtExpr.Expr);
    }

    private void GenStmtLet(IRBasicBlock block, HIRStmtLet stmtLet)
    {
        IRValue value = GenExprValue(block, stmtLet.Init);
        IRValue addr = LookupValue(stmtLet.VariableSymbol);

        IRInstructionStore store = new()
        {
            Value = value,
            Address = addr,
        };
        block.Add(store);
    }

    private void GenStmtReturn(IRBasicBlock block, HIRStmtReturn stmtReturn)
    {
        IRValue? value = null;
        if (stmtReturn.Value != null)
        {
            value = GenExprValue(block, stmtReturn.Value);
        }

        IRInstructionRet ret = new()
        {
            Value = value,
        };
        block.Add(ret);
    }

    private IRValue GenExprValue(IRBasicBlock block, HIRExpr expr)
    {
        if (expr.IsLValue)
        {
            throw new InvalidHIRException("LValue must be loaded");
        }

        switch (expr)
        {
            case HIRExprBinary hirExprBinary:
                return GenExprBinaryValue(block, hirExprBinary);
            case HIRExprCall hirExprCall:
                return GenExprCallValue(block, hirExprCall);
            case HIRExprCast hirExprCast:
                return GenExprCastValue(block, hirExprCast);
            case HIRExprFuncRef hirExprFuncRef:
                return GenExprFuncRefValue(block, hirExprFuncRef);
            case HIRExprIntConst hirExprIntConst:
                return GenExprIntValue(hirExprIntConst);
            case HIRExprLoad hirExprLoad:
                return GenExprLoadValue(block, hirExprLoad);
            case HIRExprUnary hirExprUnary:
                return GenExprUnaryValue(block, hirExprUnary);
            case HIRExprZeroInit hirExprZeroInit:
                return GenExprZeroInitValue(hirExprZeroInit);
            case HIRExprLocalRef:
                throw new InvalidHIRException($"Unexpected {nameof(HIRExprLocalRef)}. Must be loaded");
            case HIRExprError:
                throw new InvalidHIRException("Backend mustn't be run on errors");
            default:
                throw new UnreachableException();
        }
    }

    private IRValue GenExprLoadValue(IRBasicBlock block, HIRExprLoad expr)
    {
        if (!expr.Address.IsLValue)
        {
            throw new InvalidHIRException("Cannot load non-lvalue");
        }

        IRValue addr = GenExprAddr(block, expr.Address);
        IRType type = _lowerer.ToLowerType(expr.Type);
        IRInstructionLoad load = new()
        {
            LoadedType = type,
            Address = addr,
        };
        block.Add(load);
        return load;
    }

    private IRValue GenExprUnaryValue(IRBasicBlock block, HIRExprUnary expr)
    {
        IRValue operand = GenExprValue(block, expr.Operand);
        UnaryOp op = expr.Op;
        SpamType operandsType = expr.Operand.Type;
        switch (operandsType.Kind)
        {
            case TypeKind.SignedInteger:
                return GenUnaryOpSigned(block, operand, op);
            case TypeKind.UnsignedInteger:
                return GenUnaryOpUnsigned(block, operand, op);
            case TypeKind.Float:
                return GenUnaryOpFloat(block, operand, op);
            case TypeKind.Bool:
                return GenUnaryOpBool(block, operand, op);
            case TypeKind.Error:
            case TypeKind.Void:
            case TypeKind.AbstractNumber:
            case TypeKind.Function:
                throw new InvalidHIRException($"Unexpected type: {operandsType.Name}");
            default:
                throw new UnreachableException();
        }
    }

    private IRValue GenUnaryOpSigned(IRBasicBlock block, IRValue operand, UnaryOp op)
    {
        IRValue AddUnaryInstruction(IRUnaryOp unaryOp)
        {
            IRInstructionUnary instr = new()
            {
                Value = operand,
                Op = unaryOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case UnaryOp.Plus: return operand;
            case UnaryOp.Minus: return AddUnaryInstruction(IRUnaryOp.Neg);
            case UnaryOp.BitNot: return AddUnaryInstruction(IRUnaryOp.BitNot);
            case UnaryOp.LogicNot: throw MakeCannotPerformOpException(op, "signed integer");

            default: throw new UnreachableException();
        }
    }

    private IRValue GenUnaryOpUnsigned(IRBasicBlock block, IRValue operand, UnaryOp op)
    {
        IRValue AddUnaryInstruction(IRUnaryOp unaryOp)
        {
            IRInstructionUnary instr = new()
            {
                Value = operand,
                Op = unaryOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case UnaryOp.Plus: return operand;
            case UnaryOp.Minus: throw MakeCannotPerformOpException(op, "unsigned integer");
            case UnaryOp.BitNot: return AddUnaryInstruction(IRUnaryOp.BitNot);
            case UnaryOp.LogicNot: throw MakeCannotPerformOpException(op, "unsigned integer");

            default: throw new UnreachableException();
        }
    }

    private IRValue GenUnaryOpFloat(IRBasicBlock block, IRValue operand, UnaryOp op)
    {
        IRValue AddUnaryInstruction(IRUnaryOp unaryOp)
        {
            IRInstructionUnary instr = new()
            {
                Value = operand,
                Op = unaryOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case UnaryOp.Plus: return operand;
            case UnaryOp.Minus: return AddUnaryInstruction(IRUnaryOp.FNeg);
            case UnaryOp.BitNot:
            case UnaryOp.LogicNot: throw MakeCannotPerformOpException(op, "float");

            default: throw new UnreachableException();
        }
    }

    private IRValue GenUnaryOpBool(IRBasicBlock block, IRValue operand, UnaryOp op)
    {
        IRValue AddUnaryInstruction(IRUnaryOp unaryOp)
        {
            IRInstructionUnary instr = new()
            {
                Value = operand,
                Op = unaryOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case UnaryOp.Plus:
            case UnaryOp.Minus:
            case UnaryOp.BitNot: throw MakeCannotPerformOpException(op, "bool");
            case UnaryOp.LogicNot: return AddUnaryInstruction(IRUnaryOp.BoolNot);

            default: throw new UnreachableException();
        }
    }

    private IRValue GenExprBinaryValue(IRBasicBlock block, HIRExprBinary expr)
    {
        if (expr.Left.Type != expr.Right.Type)
        {
            throw new InvalidHIRException("Cannot perform binary operation on different types");
        }

        IRValue left = GenExprValue(block, expr.Left);
        IRValue right = GenExprValue(block, expr.Right);
        Debug.Assert(left.Type == right.Type, "Types must be the same");

        BinaryOp op = expr.Op;
        SpamType operandsType = expr.Left.Type;
        switch (operandsType.Kind)
        {
            case TypeKind.SignedInteger:
                return GenBinaryOpSigned(block, left, right, op);
            case TypeKind.UnsignedInteger:
                return GenBinaryOpUnsigned(block, left, right, op);
            case TypeKind.Float:
                return GenBinaryOpFloat(block, left, right, op);
            case TypeKind.Bool:
                return GenBinaryOpBool(block, left, right, op);
            case TypeKind.Error:
            case TypeKind.Void:
            case TypeKind.AbstractNumber:
            case TypeKind.Function:
                throw new InvalidHIRException($"Unexpected type: {operandsType.Name}");
            default:
                throw new UnreachableException();
        }
    }

    private IRValue GenBinaryOpSigned(IRBasicBlock block, IRValue left, IRValue right, BinaryOp op)
    {
        IRValue AddBinaryInstruction(IRBinaryOp binaryOp)
        {
            IRInstructionBinary instr = new()
            {
                Left = left,
                Right = right,
                Op = binaryOp,
            };
            block.Add(instr);
            return instr;
        }

        IRValue AddCompareInstruction(IRCompareOp compareOp)
        {
            IRInstructionCompare instr = new()
            {
                Left = left,
                Right = right,
                Op = compareOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case BinaryOp.Plus: return AddBinaryInstruction(IRBinaryOp.Add);
            case BinaryOp.Minus: return AddBinaryInstruction(IRBinaryOp.Sub);
            case BinaryOp.Mul: return AddBinaryInstruction(IRBinaryOp.Mul);
            case BinaryOp.Div: return AddBinaryInstruction(IRBinaryOp.SDiv);
            case BinaryOp.Rem: return AddBinaryInstruction(IRBinaryOp.SRem);

            case BinaryOp.BitAnd: return AddBinaryInstruction(IRBinaryOp.And);
            case BinaryOp.BitOr: return AddBinaryInstruction(IRBinaryOp.Or);
            case BinaryOp.BitXor: return AddBinaryInstruction(IRBinaryOp.Xor);
            case BinaryOp.BitShiftLeft: return AddBinaryInstruction(IRBinaryOp.ShiftLeft);
            case BinaryOp.BitShiftRight: return AddBinaryInstruction(IRBinaryOp.ArithmeticShiftRight);

            case BinaryOp.LogicAnd:
            case BinaryOp.LogicOr: throw MakeCannotPerformOpException(op, "signed integer");

            case BinaryOp.Equal: return AddCompareInstruction(IRCompareOp.Equal);
            case BinaryOp.NotEqual: return AddCompareInstruction(IRCompareOp.NotEqual);
            case BinaryOp.Less: return AddCompareInstruction(IRCompareOp.SLess);
            case BinaryOp.LessEqual: return AddCompareInstruction(IRCompareOp.SLessEqual);
            case BinaryOp.Greater: return AddCompareInstruction(IRCompareOp.SGreater);
            case BinaryOp.GreaterEqual: return AddCompareInstruction(IRCompareOp.SGreaterEqual);

            default: throw new UnreachableException();
        }
    }

    private IRValue GenBinaryOpUnsigned(IRBasicBlock block, IRValue left, IRValue right, BinaryOp op)
    {
        IRValue AddBinaryInstruction(IRBinaryOp binaryOp)
        {
            IRInstructionBinary instr = new()
            {
                Left = left,
                Right = right,
                Op = binaryOp,
            };
            block.Add(instr);
            return instr;
        }

        IRValue AddCompareInstruction(IRCompareOp compareOp)
        {
            IRInstructionCompare instr = new()
            {
                Left = left,
                Right = right,
                Op = compareOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case BinaryOp.Plus: return AddBinaryInstruction(IRBinaryOp.Add);
            case BinaryOp.Minus: return AddBinaryInstruction(IRBinaryOp.Sub);
            case BinaryOp.Mul: return AddBinaryInstruction(IRBinaryOp.Mul);
            case BinaryOp.Div: return AddBinaryInstruction(IRBinaryOp.UDiv);
            case BinaryOp.Rem: return AddBinaryInstruction(IRBinaryOp.URem);

            case BinaryOp.BitAnd: return AddBinaryInstruction(IRBinaryOp.And);
            case BinaryOp.BitOr: return AddBinaryInstruction(IRBinaryOp.Or);
            case BinaryOp.BitXor: return AddBinaryInstruction(IRBinaryOp.Xor);
            case BinaryOp.BitShiftLeft: return AddBinaryInstruction(IRBinaryOp.ShiftLeft);
            case BinaryOp.BitShiftRight: return AddBinaryInstruction(IRBinaryOp.LogicalShiftRight);

            case BinaryOp.LogicAnd:
            case BinaryOp.LogicOr: throw MakeCannotPerformOpException(op, "unsigned integer");

            case BinaryOp.Equal: return AddCompareInstruction(IRCompareOp.Equal);
            case BinaryOp.NotEqual: return AddCompareInstruction(IRCompareOp.NotEqual);
            case BinaryOp.Less: return AddCompareInstruction(IRCompareOp.ULess);
            case BinaryOp.LessEqual: return AddCompareInstruction(IRCompareOp.ULessEqual);
            case BinaryOp.Greater: return AddCompareInstruction(IRCompareOp.UGreater);
            case BinaryOp.GreaterEqual: return AddCompareInstruction(IRCompareOp.UGreaterEqual);

            default: throw new UnreachableException();
        }
    }

    private IRValue GenBinaryOpFloat(IRBasicBlock block, IRValue left, IRValue right, BinaryOp op)
    {
        IRValue AddBinaryInstruction(IRBinaryOp binaryOp)
        {
            IRInstructionBinary instr = new()
            {
                Left = left,
                Right = right,
                Op = binaryOp,
            };
            block.Add(instr);
            return instr;
        }

        IRValue AddCompareInstruction(IRCompareOp compareOp)
        {
            IRInstructionCompare instr = new()
            {
                Left = left,
                Right = right,
                Op = compareOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case BinaryOp.Plus: return AddBinaryInstruction(IRBinaryOp.FAdd);
            case BinaryOp.Minus: return AddBinaryInstruction(IRBinaryOp.FSub);
            case BinaryOp.Mul: return AddBinaryInstruction(IRBinaryOp.FMul);
            case BinaryOp.Div: return AddBinaryInstruction(IRBinaryOp.FDiv);

            case BinaryOp.Rem: throw MakeCannotPerformOpException(op, "float");

            case BinaryOp.BitAnd:
            case BinaryOp.BitOr:
            case BinaryOp.BitXor:
            case BinaryOp.BitShiftLeft:
            case BinaryOp.BitShiftRight: throw MakeCannotPerformOpException(op, "float");

            case BinaryOp.LogicAnd:
            case BinaryOp.LogicOr: throw MakeCannotPerformOpException(op, "float");

            case BinaryOp.Equal: return AddCompareInstruction(IRCompareOp.FEqual);
            case BinaryOp.NotEqual: return AddCompareInstruction(IRCompareOp.FNotEqual);
            case BinaryOp.Less: return AddCompareInstruction(IRCompareOp.FLess);
            case BinaryOp.LessEqual: return AddCompareInstruction(IRCompareOp.FLessEqual);
            case BinaryOp.Greater: return AddCompareInstruction(IRCompareOp.FGreater);
            case BinaryOp.GreaterEqual: return AddCompareInstruction(IRCompareOp.FGreaterEqual);

            default: throw new UnreachableException();
        }
    }

    private IRValue GenBinaryOpBool(IRBasicBlock block, IRValue left, IRValue right, BinaryOp op)
    {
        IRValue AddBinaryInstruction(IRBinaryOp binaryOp)
        {
            IRInstructionBinary instr = new()
            {
                Left = left,
                Right = right,
                Op = binaryOp,
            };
            block.Add(instr);
            return instr;
        }

        IRValue AddCompareInstruction(IRCompareOp compareOp)
        {
            IRInstructionCompare instr = new()
            {
                Left = left,
                Right = right,
                Op = compareOp,
            };
            block.Add(instr);
            return instr;
        }

        switch (op)
        {
            case BinaryOp.Plus:
            case BinaryOp.Minus:
            case BinaryOp.Mul:
            case BinaryOp.Div:
            case BinaryOp.Rem: throw MakeCannotPerformOpException(op, "bool");

            case BinaryOp.BitAnd:
            case BinaryOp.BitOr:
            case BinaryOp.BitXor:
            case BinaryOp.BitShiftLeft:
            case BinaryOp.BitShiftRight: throw MakeCannotPerformOpException(op, "bool");

            // TODO: Short-circuiting
            case BinaryOp.LogicAnd: return AddBinaryInstruction(IRBinaryOp.And);
            case BinaryOp.LogicOr: return AddBinaryInstruction(IRBinaryOp.Or);

            case BinaryOp.Equal: return AddCompareInstruction(IRCompareOp.Equal);
            case BinaryOp.NotEqual: return AddCompareInstruction(IRCompareOp.NotEqual);
            case BinaryOp.Less:
            case BinaryOp.LessEqual:
            case BinaryOp.Greater:
            case BinaryOp.GreaterEqual: throw MakeCannotPerformOpException(op, "bool");

            default: throw new UnreachableException();
        }
    }

    private IRValue GenExprCallValue(IRBasicBlock block, HIRExprCall expr)
    {
        FuncType funcType = (FuncType)expr.Callee.Type;

        IRValue callee = GenExprValue(block, expr.Callee);

        // NOTE: IR args are in order of FUNCTION PARAMETERS, not call expression args
        List<IRValue?> args = new();
        args.EnsureCapacity(funcType.ParamTypes.Count);
        for (int i = 0; i < funcType.ParamTypes.Count; i++)
        {
            args.Add(null);
        }

        foreach (HIRCallArg arg in expr.Args)
        {
            if (args[arg.ParameterIndex] != null)
            {
                throw new InvalidHIRException($"Duplicate argument {arg.ParameterIndex} in call");
            }

            args[arg.ParameterIndex] = GenExprValue(block, arg.Value);
        }

        if (args.Contains(null))
        {
            throw new InvalidHIRException("Not all arguments were set in call");
        }

        IRSignature lowerSignature = _lowerer.ToLowerSignature(funcType);
        IRInstructionCall call = new()
        {
            Args = args!,
            Callee = callee,
            LoweredSignature = lowerSignature,
        };
        block.Add(call);
        return call;
    }

    private IRValue GenExprCastValue(IRBasicBlock block, HIRExprCast expr)
    {
        IRValue value = GenExprValue(block, expr.Value);

        IRValue AddCastInstruction(IRCastOp castOp)
        {
            IRType lowered = _lowerer.ToLowerType(expr.Type);
            IRInstructionCast instr = new()
            {
                Value = value,
                Op = castOp,
                CastTo = lowered,
            };
            block.Add(instr);
            return instr;
        }

        SpamType origType = expr.Value.Type;
        SpamType targetType = expr.Type;

        if (origType.Kind == targetType.Kind && origType.Size == targetType.Size)
        {
            return value;
        }


        // Truncate,
        // SignExtend,
        // ZeroExtend,
        //
        // FloatExtend,
        // FloatTruncate,
        //
        // UnsignedToFloat,
        // SignedToFloat,
        //
        // FloatToUnsigned,
        // FloatToSigned,

        bool extended = targetType.Size > origType.Size;
        switch (origType.Kind)
        {
            case TypeKind.SignedInteger:
                switch (targetType.Kind)
                {
                    case TypeKind.SignedInteger:
                        return AddCastInstruction(extended ? IRCastOp.SignExtend : IRCastOp.Truncate);
                    case TypeKind.UnsignedInteger:
                        return AddCastInstruction(extended ? IRCastOp.ZeroExtend : IRCastOp.Truncate);
                    case TypeKind.Float:
                        return AddCastInstruction(IRCastOp.SignedToFloat);
                    default:
                        throw new InvalidHIRException($"Unexpected type: {origType.Name}");
                }
            case TypeKind.UnsignedInteger:
                switch (targetType.Kind)
                {
                    case TypeKind.SignedInteger:
                        break;
                    case TypeKind.UnsignedInteger:
                        break;
                    case TypeKind.Float:
                        break;
                    default:
                        throw new InvalidHIRException($"Unexpected type: {origType.Name}");
                }
            case TypeKind.Bool:
                switch (targetType.Kind)
                {
                    case TypeKind.SignedInteger:
                        break;
                    case TypeKind.UnsignedInteger:
                        break;
                    case TypeKind.Float:
                        break;
                    default:
                        throw new InvalidHIRException($"Unexpected type: {origType.Name}");
                }
            case TypeKind.Float:
                switch (targetType.Kind)
                {
                    case TypeKind.SignedInteger:
                        break;
                    case TypeKind.UnsignedInteger:
                        break;
                    case TypeKind.Float:
                        break;
                    default:
                        throw new InvalidHIRException($"Unexpected type: {origType.Name}");
                }
            default:
                throw new InvalidHIRException($"Unexpected type: {origType.Name}");
        }
    }

    private IRValue GenExprFuncRefValue(IRBasicBlock block, HIRExprFuncRef expr)
    {
        IRValue value = LookupValue(expr.Symbol);
        return value;
    }

    private IRValue GenExprIntValue(HIRExprIntConst expr)
    {
        IRType type = _lowerer.ToLowerType(expr.Type);
        IRConstantInt value = IRConstantInt.Create(expr.Value, type);
        return value;
    }

    private IRValue GenExprZeroInitValue(HIRExprZeroInit expr)
    {
        IRType type = _lowerer.ToLowerType(expr.Type);
        IRValue value = MakeZeroInitialized(type);
        return value;
    }

    private IRValue GenExprAddr(IRBasicBlock block, HIRExpr expr)
    {
        if (!expr.IsLValue)
        {
            throw new InvalidHIRException("Cannot take address of non-lvalue");
        }

        switch (expr)
        {
            case HIRExprLocalRef locRef:
                IRValue value = LookupValue(locRef.Symbol);
                return value;
            default:
                throw new UnreachableException();
        }
    }

    private IRValue MakeZeroInitialized(IRType type)
    {
        Debug.Assert(type.Kind != IRTypeKind.Void, "Unexpected void type in MakeZeroInitialized");

        switch (type.Kind)
        {
            case IRTypeKind.Int:
            case IRTypeKind.Ptr:
            case IRTypeKind.Bool:
                return IRConstantInt.Create(0, type);
            case IRTypeKind.Float:
                return new IRConstantFloat
                {
                    Value = 0.0,
                    FloatType = type,
                };
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void GenLocals(IRBasicBlock entry, IReadOnlyList<VariableSymbol> locals)
    {
        foreach (VariableSymbol sym in locals)
        {
            IRType type = _lowerer.ToLowerType(sym.Type);

            IRInstructionAlloca alloca = new()
            {
                AllocatedType = type,
            };
            entry.Add(alloca);

            if (TryLookupValue(sym) != null)
            {
                throw new InvalidHIRException($"Redeclaration of symbol {sym.Name}");
            }

            CurrentSymScope().Add(sym, alloca);
        }
    }

    private void GenParams(IRBasicBlock entry, IReadOnlyList<ParamSymbol> funcParams, List<IRParam> irParams)
    {
        Debug.Assert(irParams.Count == 0);

        int initialNumInstructions = entry.Instructions.Count;
        foreach (ParamSymbol sym in funcParams)
        {
            IRType type = _lowerer.ToLowerType(sym.Type);
            IRParam irParam = new()
            {
                ParamType = type,
                Index = irParams.Count,
            };
            irParams.Add(irParam);

            IRInstructionAlloca alloca = new()
            {
                AllocatedType = type,
            };
            entry.Add(alloca);

            if (TryLookupValue(sym) != null)
            {
                throw new InvalidHIRException($"Redeclaration of symbol {sym.Name}");
            }

            CurrentSymScope().Add(sym, alloca);
        }

        for (int i = 0; i < irParams.Count; i++)
        {
            IRParam param = irParams[i];
            IRInstruction alloca = entry.Instructions[initialNumInstructions + i];
            Debug.Assert(alloca is IRInstructionAlloca);
            IRInstructionStore store = new()
            {
                Value = param,
                Address = alloca,
            };
            entry.Add(store);
        }
    }

    private void PushSymScope(Dictionary<Symbol, IRValue> scope)
    {
        _symbolScopes.Add(scope);
    }

    private void PopSymScope()
    {
        _symbolScopes.RemoveAt(_symbolScopes.Count - 1);
    }

    private Dictionary<Symbol, IRValue> CurrentSymScope()
    {
        return _symbolScopes[^1];
    }

    private IRValue LookupValue(Symbol symbol)
    {
        IRValue? value = TryLookupValue(symbol);
        if (value == null)
        {
            throw new InvalidHIRException($"Cannot find symbol {symbol.Name}");
        }

        return value;
    }

    private IRValue? TryLookupValue(Symbol symbol)
    {
        for (int i = _symbolScopes.Count - 1; i >= 0; --i)
        {
            Dictionary<Symbol, IRValue> scope = _symbolScopes[i];
            if (scope.TryGetValue(symbol, out IRValue? value))
            {
                return value;
            }
        }

        return null;
    }

    private void AllocateIds(IRModule module)
    {
        foreach (IRFunction func in module.Functions)
        {
            int cur = -1;
            for (int i = 0; i < func.Params.Count; i++)
            {
                IRParam param = func.Params[i];
                param.Id = ++cur;
            }

            foreach (IRBasicBlock bb in func.BasicBlocks)
            {
                foreach (IRInstruction inst in bb.Instructions)
                {
                    if (inst.Type != IRType.Void)
                    {
                        inst.Id = ++cur;
                    }
                }
            }
        }
    }

    private static InvalidHIRException MakeCannotPerformOpException(BinaryOp op, string type)
    {
        return new InvalidHIRException($"Cannot perform operation \"{op.AsString()}\" on {type}");
    }

    private static InvalidHIRException MakeCannotPerformOpException(UnaryOp op, string type)
    {
        return new InvalidHIRException($"Cannot perform operation \"{op.AsString()}\" on {type}");
    }
}