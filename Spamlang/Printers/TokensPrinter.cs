namespace Spamlang.Printers;

public class TokensPrinter
{
    public static void Print(List<Token> tokens, string code)
    {
        foreach (Token token in tokens)
        {
            Console.WriteLine(token.ToString(code));
        }
    }

    public static void PrintPretty(List<Token> tokens, string code)
    {
        int curLine = 0;
        int curColumn = 1;
        foreach (Token token in tokens)
        {
            while (curLine < token.Line)
            {
                curLine++;
                curColumn = 1;
                Console.WriteLine();
                Console.Write($"{curLine,5}:  ");
            }

            if (curColumn >= token.Column)
            {
                Console.Write(" ");
                curColumn = token.Column;
            }

            while (curColumn < token.Column)
            {
                Console.Write(" ");
                curColumn++;
            }

            string str = token.Type.PrettyName();

            if (token.Type.IsLiteral || token.Type == TokenType.Identifier)
            {
                str += token.Value(code).ToString();
            }

            Console.Write(str);
            curColumn += str.Length;
        }

        Console.WriteLine();
    }
}