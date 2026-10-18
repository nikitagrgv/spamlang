namespace Spamlang.Backend;

public class FuncABI
{
    public required List<MValueLocation> ParamLocations { get; init; }
    public required MValueLocation? ReturnLocation { get; init; }
}

public interface ITargetABI
{
    public FuncABI Generate(IRSignature signature);
}