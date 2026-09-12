namespace Spamlang;

public sealed class IRModule
{
    public required List<IRFunction> Functions { get; init; }
}

public sealed class IRFunction : IRValue
{
    public required string Name { get; init; }
    public required FuncType Signature { get; init; }
    public required List<IRParam> Params { get; init; }
    public required List<IRBasicBlock> BasicBlocks { get; init; }

    public override Type Type => BuiltinType.Ptr;

    public override string PrintOperand()
    {
        return $"{Type} @{Name}";
    }

    public override string PrintDefinition()
    {
        return $"{Signature} @{Name}";
    }
}

public sealed class IRBasicBlock
{
    public required string Name { get; init; }
    public required List<IRInstruction> Instructions { get; init; }

    public void Add(IRInstruction instruction)
    {
        instruction.Parent = this;
        Instructions.Add(instruction);
    }

    public IRInstruction? Terminator
    {
        get
        {
            if (Instructions.Count == 0)
            {
                return null;
            }

            IRInstruction last = Instructions[^1];
            if (!last.IsTerminator)
            {
                return null;
            }

            return last;
        }
    }
}

public abstract class IRValue
{
    public abstract Type Type { get; }
    public int Id { get; set; } = -1;

    public virtual string PrintOperand()
    {
        return $"{Type} %{Id}";
    }

    public virtual string PrintDefinition()
    {
        return PrintOperand();
    }
}

public sealed class IRConstantInt : IRValue
{
    public required Type IntType { get; init; }
    public required Int128 Value { get; init; }

    public override Type Type => IntType;

    public override string PrintOperand()
    {
        return $"{Type} {Value}";
    }
}

public sealed class IRParam : IRValue
{
    public required Type ParamType { get; init; }
    public required int Index { get; init; }

    public override Type Type => ParamType;
}

public abstract class IRInstruction : IRValue
{
    public IRBasicBlock? Parent { get; set; }
    public virtual bool IsTerminator => false;
}

public sealed class IRInstructionRet : IRInstruction
{
    public required IRValue? Value { get; init; }
    public override Type Type => BuiltinType.Void;
    public override bool IsTerminator => true;

    public override string PrintDefinition()
    {
        if (Value == null)
        {
            return "ret";
        }

        return $"ret {Value.PrintOperand()}";
    }
}

public sealed class IRInstructionAlloca : IRInstruction
{
    public required Type AllocatedType { get; init; }
    public override Type Type => BuiltinType.Ptr;

    public override string PrintDefinition()
    {
        return $"%{Id} = alloca {AllocatedType}";
    }
}

public sealed class IRInstructionLoad : IRInstruction
{
    public required Type LoadedType { get; init; }
    public required IRValue Address { get; init; }
    public override Type Type => LoadedType;

    public override string PrintDefinition()
    {
        return $"%{Id} = load {LoadedType}, {Address.PrintOperand()}";
    }
}

public sealed class IRInstructionStore : IRInstruction
{
    public required IRValue Value { get; init; }
    public required IRValue Address { get; init; }
    public override Type Type => BuiltinType.Void;

    public override string PrintDefinition()
    {
        return $"store {Value.PrintOperand()}, {Address.PrintOperand()}";
    }
}

public enum IRBinaryOp
{
    Add,
    Sub,
    Mul,
    SDiv,
    UDiv,
    SRem,
    URem,
}

public sealed class IRInstructionBinary : IRInstruction
{
    public required IRBinaryOp Op { get; init; }
    public required IRValue Left { get; init; }
    public required IRValue Right { get; init; }
    public override Type Type => Left.Type;

    public override string PrintDefinition()
    {
        return $"%{Id} = {Op} {Left.PrintOperand()}, {Right.PrintOperand()}";
    }
}

public sealed class IRInstructionCall : IRInstruction
{
    public required IRValue Callee { get; init; }
    public required FuncType Signature { get; init; }

    public required List<IRValue> Args { get; init; }

    public override Type Type => Signature.ReturnType;

    public override string PrintDefinition()
    {
        string ret = "";
        if (Type != BuiltinType.Void)
        {
            ret += $"%{Id} = ";
        }

        string name;
        if (Callee is IRFunction func)
        {
            name = $"@{func.Name}";
        }
        else
        {
            name = $"{Callee.PrintOperand()}";
        }

        ret += $"call {Type} {name}({string.Join(", ", Args.Select(a => a.PrintOperand()))})";
        return ret;
    }
}

public sealed class IRInstructionCast : IRInstruction
{
    public required IRValue Value { get; init; }
    public required Type CastTo { get; init; }
    public override Type Type => CastTo;

    public override string PrintDefinition()
    {
        return $"%{Id} = cast {CastTo}, {Value.PrintOperand()}";
    }
}