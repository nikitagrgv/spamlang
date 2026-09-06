namespace Compiler;

public class Scope
{
    private readonly Dictionary<string, Symbol> _symbols = new();
    private readonly Dictionary<string, Symbol>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public Scope? Parent { get; init; }

    public Scope(Scope? parent)
    {
        Parent = parent;
        _lookup = _symbols.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public Symbol? LookupLocal(ReadOnlySpan<char> name)
    {
        _lookup.TryGetValue(name, out Symbol? symbol);
        if (symbol != null)
        {
            return symbol;
        }

        return null;
    }

    public Symbol? LookupRecursive(ReadOnlySpan<char> name)
    {
        Scope? cur = this;
        while (cur != null)
        {
            Symbol? s = cur.LookupLocal(name);
            if (s != null)
            {
                return s;
            }

            cur = cur.Parent;
        }

        return null;
    }

    public bool TryDeclare(Symbol symbol)
    {
        bool added = _symbols.TryAdd(symbol.Name, symbol);
        return added;
    }
}