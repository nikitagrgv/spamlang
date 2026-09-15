namespace Spamlang.Lsp;

public class Server
{
    private Stream _input;
    private Stream _output;

    public Server(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public void Run()
    {
    }
}