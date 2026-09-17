namespace Spamlang.Frontend;

public abstract class Symbol
{
    public required string Name { get; init; }
    public abstract SpamType Type { get; }

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
    public required SpamType VariableType { get; init; }

    public override SpamType Type => VariableType;
    public override Node? DeclaringNode => Declaration;
    public override string SymbolKindName => "variable";
}

public sealed class ParamSymbol : LocalSymbol
{
    public required Param Declaration { get; init; }
    public required SpamType ParamType { get; init; }

    public override SpamType Type => ParamType;
    public override Node? DeclaringNode => Declaration;
    public override string SymbolKindName => "param";
}

public sealed class FuncSymbol : Symbol
{
    public required FuncDecl Declaration { get; init; }
    public required IReadOnlyList<ParamSymbol> Params { get; init; }
    public required FuncType FuncType { get; init; }

    public override SpamType Type => FuncType.ReturnType;
    public override Node? DeclaringNode => Declaration;
    public override string SymbolKindName => "function";
}

public sealed class TypeSymbol : Symbol
{
    // Will be added with user types, e.g., structs
    // public required Node? Declaration { get; init; }
    public required SpamType SymbolType { get; init; }

    public override SpamType Type => SymbolType;
    public override Node? DeclaringNode => null;
    public override string SymbolKindName => "type";
}