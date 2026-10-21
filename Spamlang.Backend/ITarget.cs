namespace Spamlang.Backend;

public class FuncABI
{
    public required List<MValueLocation> ParamLocations { get; init; }
    public required MValueLocation? ReturnLocation { get; init; }
}

public interface ITarget
{
    public FuncABI GenerateFuncABI(IRSignature signature);
    public string GetAsmTargetName();
}