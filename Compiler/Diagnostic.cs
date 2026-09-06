namespace Compiler;

public enum DiagnosticSeverity
{
    Error,
    Warning
}

// TODO: Add filename?
public readonly struct DiagnosticEntry
{
    public required DiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }

    public required int Position { get; init; }
    public required int Length { get; init; }

    public required int Line { get; init; }
    public required int Column { get; init; }

    public string PrettyString()
    {
        string sev = Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => throw new NotImplementedException()
        };

        return $"{sev} at {Line}:{Column}: {Message}";
    }
}

public class Diagnostic
{
    private readonly List<DiagnosticEntry> _entries = [];

    public IReadOnlyList<DiagnosticEntry> Entries => _entries;
    public bool HasErrors { get; private set; } = false;
    public bool HasAny => _entries.Count > 0;

    public void Clear()
    {
        _entries.Clear();
        HasErrors = false;
    }

    public void AddError(string message, int pos, int len, int line, int col)
    {
        DiagnosticEntry entry = new()
        {
            Severity = DiagnosticSeverity.Error,
            Message = message,
            Position = pos,
            Length = len,
            Line = line,
            Column = col,
        };
        _entries.Add(entry);
        HasErrors = true;
    }

    public void AddError(string message, Token token)
    {
        AddError(message, token.Position, token.Length, token.Line, token.Column);
    }

    public void AddWarning(string message, int pos, int len, int line, int col)
    {
        DiagnosticEntry entry = new()
        {
            Severity = DiagnosticSeverity.Warning,
            Message = message,
            Position = pos,
            Length = len,
            Line = line,
            Column = col,
        };
        _entries.Add(entry);
    }

    public void AddWarning(string message, Token token)
    {
        AddWarning(message, token.Position, token.Length, token.Line, token.Column);
    }

    public void Report()
    {
        foreach (DiagnosticEntry entry in _entries)
        {
            Console.WriteLine(entry.PrettyString());
        }
    }
}