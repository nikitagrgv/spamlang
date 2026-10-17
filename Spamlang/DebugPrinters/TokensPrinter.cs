using Spamlang.Frontend;

namespace Spamlang.DebugPrinters;

public class TokensPrinter
{
    private readonly TextWriter _writer;

    public static void Print(List<Token> tokens, string code, TextWriter writer)
    {
        TokensPrinter printer = new(writer);
        printer.Print(tokens, code);
    }

    public static void PrintPretty(List<Token> tokens, string code, TextWriter writer)
    {
        TokensPrinter printer = new(writer);
        printer.PrintPretty(tokens, code);
    }

    private TokensPrinter(TextWriter writer)
    {
        _writer = writer;
    }

    private void Print(List<Token> tokens, string code)
    {
        foreach (Token token in tokens)
        {
            _writer.WriteLine(token.ToString(code));
        }
    }

    private void PrintPretty(List<Token> tokens, string code)
    {
        int curLine = 0;
        int curColumn = 1;
        foreach (Token token in tokens)
        {
            while (curLine < token.Line)
            {
                curLine++;
                curColumn = 1;
                _writer.WriteLine();
                _writer.Write($"{curLine,5}:  ");
            }

            if (curColumn >= token.Column)
            {
                _writer.Write(" ");
                curColumn = token.Column;
            }

            while (curColumn < token.Column)
            {
                _writer.Write(" ");
                curColumn++;
            }

            string str = token.Type.PrettyName();

            if (token.Type.IsLiteral || token.Type == TokenType.Identifier)
            {
                str += token.Value(code).ToString();
            }

            _writer.Write(str);
            curColumn += str.Length;
        }

        _writer.WriteLine();
    }
}