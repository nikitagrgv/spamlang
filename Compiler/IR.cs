namespace Compiler;

public class IRModule
{
    public required List<IRFunction> Functions { get; init; }
}

public class IRFunction
{
    public required string Name { get; init; }
    public required FuncType Type { get; init; }
    public required List<IRParam> Params { get; init; }
    public required List<IRBasicBlock> BasicBlocks { get; init; }
}

public class IRBasicBlock
{
    public required string Name { get; init; }
    public required List<IRInstruction> Instructions { get; init; }

    public void Add(IRInstruction instruction)
    {
        instruction.Parent = this;
        Instructions.Add(instruction);
    }

    public IRInstruction? Terminator => Instructions.LastOrDefault();
}

public abstract class IRValue
{
    public abstract Type Type { get; }
    public int Id { get; set; } = -1;
}

public sealed class IRConstantInt : IRValue
{
    public required Type IntType { get; init; }
    public required Int128 Value { get; init; }

    public override Type Type => IntType;
}

public sealed class IRParam : IRValue
{
    public required Type ParamType { get; init; }
    public required int Index { get; init; }

    public override Type Type => ParamType;
}

public abstract class IRInstruction : IRValue
{
    public required IRBasicBlock? Parent { get; set; }
    public virtual bool IsTerminator => false;
}

public sealed class IRInstructionRet : IRInstruction
{
    public required IRValue? Value { get; init; }
    public override Type Type => BuiltinType.Void;
    public override bool IsTerminator => true;
}

public sealed class IRInstructionAlloca : IRInstruction
{
    public required Type AllocatedType { get; init; }
    public override Type Type => BuiltinType.Ptr;
}

public sealed class IRInstructionLoad : IRInstruction
{
    public required Type LoadedType { get; init; }
    public required IRValue Address { get; init; }
    public override Type Type => LoadedType;
}

public sealed class IRInstructionStore : IRInstruction
{
    public required IRValue Value { get; init; }
    public required IRValue Address { get; init; }
    public override Type Type => BuiltinType.Void;
}