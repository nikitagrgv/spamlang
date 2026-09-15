namespace Spamlang.Lsp;

public class App
{
    public static int Main(string[] args)
    {
        Stream input = new BufferedStream(Console.OpenStandardInput());
        Stream output = Console.OpenStandardOutput();
        Console.SetOut(Console.Error);

        Server server = new(input, output);
        try
        {
            server.Run();
        }
        catch (Exception)
        {
            return 1;
        }

        return 0;
    }
}