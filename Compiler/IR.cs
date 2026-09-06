namespace Compiler;

public class IRModule
{
    public required List<IRFunction> Functions { get; init; }
}

public class IRFunction
{
    public required FuncType Type { get; init; }
    public required List<IRBasicBlock> BasicBlocks { get; init; }
}

public class IRBasicBlock
{
    public required List<IRInstruction> Instructions { get; init; }
}

public abstract class IRValue
{
    public required Type Type { get; init; }
    public int Id { get; set; }
}

public sealed class IRConstantInt : IRValue
{
    public required Int128 Value { get; init; }
}

public sealed class IRParam : IRValue
{
    public required int Index { get; init; }
}

public abstract class IRInstruction : IRValue
{
    public required IRBasicBlock Parent { get; set; }
}

public sealed class IRInstructionStore : IRInstruction
{
}