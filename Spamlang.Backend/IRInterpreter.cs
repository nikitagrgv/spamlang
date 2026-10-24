namespace Spamlang.Backend;

public class IRInterpreterException : Exception
{
    public IRInterpreterException(string message) : base(message)
    {
    }
}

public class IRFunctionNotFoundException : IRInterpreterException
{
    public IRFunctionNotFoundException(string name) : base($"Function {name} not found")
    {
    }
}

public class IRSignatureDoesntMatchException : IRInterpreterException
{
    public IRSignatureDoesntMatchException(IRFunction func) : base($"Unexpected signature for {func.Name}. Expected {func.LoweredSignature}")
    {
    }
}

public class IRInterpreter
{
    IRModule _module;
    Dictionary<IRFunction, Delegate> _overrides = new();

    public IRInterpreter(IRModule module)
    {
        _module = module;
    }

    public void OverrideFunction(string name, Delegate impl)
    {
        impl.DynamicInvoke((int)32);
        IRFunction? func = FindFunction(name);
        if (func == null)
        {
            throw new IRFunctionNotFoundException(name);
        }
    }

    // TODO: Support calling functions with arguments and return values
    public void RunFunction(string name)
    {
        IRFunction? func = FindFunction(name);
        if (func == null)
        {
            throw new IRFunctionNotFoundException(name);
        }

        if (func.LoweredSignature.ReturnType != IRType.Void || func.LoweredSignature.ParamTypes.Count != 0)
        {
            throw new IRSignatureDoesntMatchException(func);
        }
        
        // TODO#
    }

    private IRFunction? FindFunction(string name)
    {
        foreach (IRFunction func in _module.Functions)
        {
            if (func.Name == name)
            {
                return func;
            }
        }

        return null;
    }
}