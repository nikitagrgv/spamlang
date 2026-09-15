namespace Spamlang.Lsp;

public class App
{
    public static int Main()
    {
        Stream input = new BufferedStream(Console.OpenStandardInput());
        Stream output = Console.OpenStandardOutput();
        Console.SetOut(Console.Error);

        Server server = new(input, output);
        try
        {
            server.Run();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }

        return 0;
    }
}