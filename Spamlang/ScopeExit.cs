namespace Spamlang;

internal class ScopeExit : IDisposable
{
    private readonly Action? _action;

    public ScopeExit(Action? action)
    {
        _action = action;
    }

    public void Dispose()
    {
        _action?.Invoke();
    }
}