namespace Spamlang;

public struct Token
{
    public required TokenType Type;

    public required int Position;
    public required int Length;

    public required int Line;
    public required int Column;

    public bool IsInvalid() => Type == TokenType.Invalid;

    public ReadOnlySpan<char> Value(string code)
    {
        return code.AsSpan(Position, Length);
    }

    public string ToString(string code)
    {
        return $"{Line}:{Column} {Type} \"{Value(code)}\"";
    }
}