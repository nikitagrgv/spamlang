namespace Spamlang;

public class FuncABI
{
    public required List<MValueLocation> ParamLocations { get; init; }
    public required MValueLocation? ReturnLocation { get; init; }
}

// TODO: Full ABI for params! stack/floats/structs
// TODO: SRet ABI (should probably be in IR)
// TODO: SystemV ABI
public sealed class FuncABIGen
{
    private static readonly Reg[] ParamsRegs = [Reg.Rcx, Reg.Rdx, Reg.R8, Reg.R9];

    public FuncABI Generate(FuncType signature)
    {
        MValueLocation? retLoc = null;
        List<MValueLocation> paramsLocs = new();

        Type retType = IRUtils.ToLowerType(signature.ReturnType);
        if (retType != BuiltinType.Void)
        {
            if (retType.Size > 8 || !IsPowerOrTwo(retType.Size) || GetClass(retType) != RegClass.Int)
            {
                throw new NotImplementedException();
            }

            retLoc = new MValueLocationRegister { Reg = Reg.Rax };
        }

        for (int i = 0; i < signature.ParamTypes.Count; i++)
        {
            if (i >= ParamsRegs.Length)
            {
                throw new NotImplementedException();
            }

            Type paramType = IRUtils.ToLowerType(signature.ParamTypes[i]);
            if (paramType.Size > 8 || !IsPowerOrTwo(paramType.Size) || GetClass(paramType) != RegClass.Int)
            {
                throw new NotImplementedException();
            }

            Reg reg = ParamsRegs[i];
            paramsLocs.Add(new MValueLocationRegister { Reg = reg });
        }

        return new FuncABI
        {
            ParamLocations = paramsLocs,
            ReturnLocation = retLoc,
        };
    }

    private static RegClass GetClass(Type type)
    {
        if (type == BuiltinType.I32 || type == BuiltinType.Ptr)
        {
            return RegClass.Int;
        }

        throw new NotImplementedException();
    }

    private static bool IsPowerOrTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }
}