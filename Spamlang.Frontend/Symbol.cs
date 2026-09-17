namespace Spamlang.Frontend;

public abstract class Symbol
{
    public required string Name { get; init; }
    public required SpamType Type { get; init; }

    public abstract Node? DeclaringNode { get; }
    public abstract string SymbolKindName { get; }

    public override string ToString()
    {
        return $"\"{Name}\"({SymbolKindName})[Type={Type}]";
    }
}

public abstract class LocalSymbol : Symbol
{
}

public sealed class VariableSymbol : LocalSymbol
{
    public required StmtLet Declaration { get; init; }

    public override Node? DeclaringNode => Declaration;
    public override string SymbolKindName => "variable";
}

public sealed class ParamSymbol : LocalSymbol
{
    public required Param Declaration { get; init; }

    public override Node? DeclaringNode => Declaration;
    public override string SymbolKindName => "param";
}

public sealed class FuncSymbol : Symbol
{
    public required FuncDecl Declaration { get; init; }

    public required IReadOnlyList<ParamSymbol> Params { get; init; }

    public override Node? DeclaringNode => Declaration;
    public override string SymbolKindName => "function";
}

public sealed class TypeSymbol : Symbol
{
    // Will be added with user types, e.g., structs
    // public required Node? Declaration { get; init; }

    public override Node? DeclaringNode => null;
    public override string SymbolKindName => "type";
}