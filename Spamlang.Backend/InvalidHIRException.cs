using Spamlang.Frontend;

namespace Spamlang.Backend;

public class InvalidHIRException : Exception
{
    public InvalidHIRException(string message) : base(message)
    {
    }
}

public class UnexpectedTypeException : InvalidHIRException
{
    public UnexpectedTypeException(SpamType type) : base($"Unexpected type: {type.Name} of kind {type.Kind}")
    {
    }
}