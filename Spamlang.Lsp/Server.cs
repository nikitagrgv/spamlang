using System.Text;
using System.Text.Json.Nodes;
using Spamlang.Frontend;

namespace Spamlang.Lsp;

public class Server
{
    private const int InternalErrorCode = -32603;
    private const int MethodNotFoundCode = -32601;

    private struct Data
    {
        public required string Code { get; init; }
        public required List<int> LineOffsets { get; init; }
        public required List<Token> Tokens { get; init; }
        public required CompilationUnit CompilationUnit { get; init; }
        public required Dictionary<int, Symbol> TokenToSymbol { get; init; }
        public required IReadOnlyList<DiagnosticEntry> Diagnostics { get; init; }
    }

    private Stream _input;
    private Stream _output;
    private bool _exit;

    private Dictionary<string, Data> _uriToData = new();

    public Server(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public void Run()
    {
        while (!_exit)
        {
            JsonNode? msg = ReadMessage();
            if (msg == null)
            {
                break;
            }

            try
            {
                HandleMessage(msg);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                if (msg["id"] is { } id && msg["method"] != null)
                {
                    ReplyError(id.DeepClone(), InternalErrorCode, e.Message);
                }
            }
        }
    }

    private JsonNode? ReadMessage()
    {
        int length = -1;
        while (true)
        {
            string? line = ReadLine(_input);
            if (line == null)
            {
                // Closed
                return null;
            }

            if (line.Length == 0)
            {
                // Empty line = header end
                break;
            }

            string contentLength = "Content-Length:";
            if (line.StartsWith(contentLength, StringComparison.OrdinalIgnoreCase))
            {
                length = int.Parse(line.AsSpan(contentLength.Length).Trim());
            }
        }

        if (length == -1)
        {
            return null;
        }

        byte[] body = new byte[length];
        _input.ReadExactly(body);
        JsonNode? node = JsonNode.Parse(body);
        return node;
    }

    private void HandleMessage(JsonNode message)
    {
        string? method = (string?)message["method"];
        JsonNode? idCopy = message["id"]?.DeepClone();
        JsonNode? paramsNode = message["params"];

        switch (method)
        {
            case "initialize":
                HandleInitialize(idCopy, paramsNode);
                break;
            case "initialized":
                HandleInitialized(idCopy, paramsNode);
                break;
            case "textDocument/didOpen":
                HandleDidOpen(idCopy, paramsNode);
                break;
            case "textDocument/didChange":
                HandleDidChange(idCopy, paramsNode);
                break;
            case "textDocument/didClose":
                HandleDidClose(idCopy, paramsNode);
                break;
            case "textDocument/hover":
                HandleHover(idCopy, paramsNode);
                break;
            case "textDocument/definition":
                HandleDefinition(idCopy, paramsNode);
                break;
            case "shutdown":
                HandleShutdown(idCopy, paramsNode);
                break;
            case "exit":
                HandleExit(idCopy, paramsNode);
                break;
            default:
                if (idCopy != null && method != null)
                {
                    ReplyError(idCopy.DeepClone(), MethodNotFoundCode, $"Method not found: {method}");
                }

                break;
        }
    }

    private void HandleInitialize(JsonNode? idCopy, JsonNode? paramsNode)
    {
        JsonObject reply = new()
        {
            ["capabilities"] = new JsonObject
            {
                ["textDocumentSync"] = 1,
                ["hoverProvider"] = true,
                ["definitionProvider"] = true,
            },
            ["serverInfo"] = new JsonObject { ["name"] = "spamlang" },
        };
        Reply(idCopy, reply);
    }

    private void HandleInitialized(JsonNode? idCopy, JsonNode? paramsNode)
    {
    }

    private void HandleDidOpen(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode!);
        string text = (string)paramsNode!["textDocument"]!["text"]!;

        Data data = RunFrontend(text);
        _uriToData[uri] = data;

        PublishDiagnostics(uri, data.Diagnostics);
    }

    private void HandleDidChange(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode!);
        string text = (string)paramsNode!["contentChanges"]!.AsArray()[^1]!["text"]!;

        Data data = RunFrontend(text);
        _uriToData[uri] = data;

        PublishDiagnostics(uri, data.Diagnostics);
    }

    private void HandleDidClose(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode!);

        _uriToData.Remove(uri);

        PublishDiagnostics(uri, []);
    }

    private void HandleHover(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode!);
        JsonNode positionNode = paramsNode!["position"]!;
        int line = (int)positionNode["line"]!;
        int character = (int)positionNode["character"]!;

        if (!_uriToData.TryGetValue(uri, out Data data))
        {
            Reply(idCopy, null);
        }

        int tokenIndex = GetTokenIndexByPos(data.Tokens, line + 1, character + 1);
        if (tokenIndex == -1)
        {
            Reply(idCopy, null);
            return;
        }

        Token token = data.Tokens[tokenIndex];
        if (!data.TokenToSymbol.TryGetValue(tokenIndex, out Symbol? symbol))
        {
            Reply(idCopy, null);
            return;
        }

        string? description = GetSymbolDescription(symbol, data.Tokens);
        if (description == null)
        {
            Reply(idCopy, null);
            return;
        }

        JsonObject result = new()
        {
            ["contents"] = new JsonObject
            {
                ["value"] = description,
                ["kind"] = "markdown",
            },
            ["range"] = new JsonObject
            {
                ["start"] = new JsonObject { ["line"] = token.Line - 1, ["character"] = token.Column - 1 },
                ["end"] = new JsonObject { ["line"] = token.Line - 1, ["character"] = token.Column + token.Length - 1 },
            },
        };

        Reply(idCopy, result);
    }

    private void HandleDefinition(JsonNode? idCopy, JsonNode? paramsNode)
    {
    }

    private void HandleShutdown(JsonNode? idCopy, JsonNode? paramsNode)
    {
        _uriToData.Clear();

        Reply(idCopy, null);
    }

    private void HandleExit(JsonNode? idCopy, JsonNode? paramsNode)
    {
        _exit = true;
    }

    private void PublishDiagnostics(string uri, IReadOnlyList<DiagnosticEntry> diags)
    {
        JsonArray diagsArray = new();
        foreach (DiagnosticEntry diag in diags)
        {
            JsonObject diagNode = new()
            {
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject { ["line"] = diag.Line - 1, ["character"] = diag.Column - 1 },
                    ["end"] = new JsonObject { ["line"] = diag.Line - 1, ["character"] = diag.Column - 1 + diag.Length },
                },
                ["severity"] = diag.Severity == DiagnosticSeverity.Error ? 1 : 2,
                ["message"] = diag.Message,
            };
            diagsArray.Add(diagNode);
        }

        JsonObject reply = new()
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "textDocument/publishDiagnostics",
            ["params"] = new JsonObject
            {
                ["uri"] = uri,
                ["diagnostics"] = diagsArray,
            },
        };
        Send(reply);
    }

    private void Reply(JsonNode? id, JsonNode? result)
    {
        JsonObject reply = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };
        Send(reply);
    }

    private void ReplyError(JsonNode id, int errorCode, string message)
    {
        JsonObject reply = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = errorCode,
                ["message"] = message,
            },
        };
        Send(reply);
    }

    private static string? ReadLine(Stream stream)
    {
        StringBuilder sb = new();
        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                // Closed
                return null;
            }

            if (b == '\n')
            {
                string line = sb.ToString();
                return line;
            }

            if (b == '\r')
            {
                continue;
            }

            sb.Append((char)b);
        }
    }

    private void Send(JsonNode message)
    {
        byte[] body = Encoding.UTF8.GetBytes(message.ToJsonString());
        _output.Write(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"));
        _output.Write(body);
        _output.Flush();
    }

    private static string ExtractUri(JsonNode paramsNode)
    {
        return (string)paramsNode["textDocument"]!["uri"]!;
    }

    private static string? GetSymbolDescription(Symbol symbol, List<Token> tokens)
    {
        StringBuilder sb = new();
        switch (symbol)
        {
            case FuncSymbol sym:
            {
                sb.Append("```spamlang\n");
                sb.Append($"fn {sym.Name}(");
                for (int i = 0; i < sym.Declaration.Params.Count; i++)
                {
                    if (i != 0)
                    {
                        sb.Append(", ");
                    }

                    Param param = sym.Declaration.Params[i];
                    sb.Append($"{param.Symbol!.Name}: {param.Symbol!.Type}");
                }

                sb.Append(")");
                if (sym.Declaration.ReturnType != null)
                {
                    sb.Append($" -> {sym.Declaration.ReturnType.ResolvedType}");
                }

                sb.Append("\n```\n\n---\n\n");

                Token declarationToken = tokens[sym.Declaration.NameToken];
                sb.Append($"Declared at line {declarationToken.Line}");

                break;
            }
            case TypeSymbol sym:
            {
                if (sym.Type is BuiltinType)
                {
                    // Nothing interesting to show
                    return null;
                }

                sb.Append("```spamlang\n");
                sb.Append($"{sym.Type}");

                if (sym.DeclaringNode != null)
                {
                    sb.Append("\n```\n\n---\n\n");
                    Token declarationToken = tokens[sym.DeclaringNode.StartToken];
                    sb.Append($"Declared at line {declarationToken.Line}");
                }

                break;
            }
            case ParamSymbol sym:
            {
                sb.Append("```spamlang\n");
                sb.Append($"{sym.Name}: {sym.Type}");
                sb.Append("\n```\n\n---\n\n");

                Token declarationToken = tokens[sym.Declaration.NameToken];
                sb.Append($"Declared at line {declarationToken.Line}");
                break;
            }
            case VariableSymbol sym:
            {
                sb.Append("```spamlang\n");
                sb.Append($"{sym.Name}: {sym.Type}");
                sb.Append("\n```\n\n---\n\n");

                Token declarationToken = tokens[sym.Declaration.NameToken];
                sb.Append($"Declared at line {declarationToken.Line}");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(symbol));
        }

        return sb.ToString();
    }

    private static int GetTokenIndexByPos(List<Token> tokens, int line, int column)
    {
        int left = 0;
        int right = tokens.Count;
        while (right - left > 1)
        {
            int middle = left + (right - left) / 2;
            Token token = tokens[middle];
            bool greater = token.Line > line || (token.Line == line && token.Column > column);
            if (greater)
            {
                right = middle;
            }
            else
            {
                left = middle;
            }
        }

        Token found = tokens[left];
        if (found.Line != line)
        {
            return -1;
        }

        if (column < found.Column || column >= found.Column + found.Length)
        {
            return -1;
        }

        return left;
    }

    private static Data RunFrontend(string code)
    {
        Diagnostic diag = new();
        TypeRegistry reg = new();
        Dictionary<int, Symbol> tokenToSymbol = new();
        Frontend.Frontend.Result result = Frontend.Frontend.Run(code, reg, diag, timers: null, tokenToSymbol);
        List<int> lineOffsets = CalcLineOffsets(code);
        return new Data
        {
            Code = code,
            LineOffsets = lineOffsets,
            Tokens = result.Tokens,
            CompilationUnit = result.CompilationUnit,
            TokenToSymbol = tokenToSymbol,
            Diagnostics = diag.Entries,
        };
    }

    private static List<int> CalcLineOffsets(string code)
    {
        List<int> result = new();
        result.Add(0);

        for (int i = 0; i < code.Length; i++)
        {
            char ch = code[i];
            if (ch == '\n')
            {
                result.Add(i + 1);
            }
        }

        return result;
    }
}