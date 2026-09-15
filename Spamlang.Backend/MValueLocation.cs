namespace Spamlang.Backend;

public abstract class MValueLocation
{
}

public sealed class MValueLocationRegister : MValueLocation
{
    public required Reg Reg { get; init; }
}

// TODO: ValueLocationStack