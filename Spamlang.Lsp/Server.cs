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
        public string Code { get; init; }
        public List<int> LineOffsets { get; init; }
        public List<Token> Tokens { get; init; }
        public CompilationUnit CompilationUnit { get; init; }
        public IReadOnlyList<DiagnosticEntry> Diagnostics { get; init; }
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
            ["capabilities"] = new JsonObject { ["textDocumentSync"] = 1 },
            ["serverInfo"] = new JsonObject { ["name"] = "spamlang" },
        };
        Reply(idCopy, reply);
    }

    private void HandleInitialized(JsonNode? idCopy, JsonNode? paramsNode)
    {
    }

    private void HandleDidOpen(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode);
        string text = (string)paramsNode!["textDocument"]!["text"]!;

        Data data = RunFrontend(text);
        _uriToData[uri] = data;
        PublishDiagnostics(uri, data.Diagnostics);
    }

    private void HandleDidChange(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode);
        string text = (string)paramsNode!["contentChanges"]!.AsArray()[^1]!["text"]!;

        Data data = RunFrontend(text);
        _uriToData[uri] = data;
        PublishDiagnostics(uri, data.Diagnostics);
    }

    private void HandleDidClose(JsonNode? idCopy, JsonNode? paramsNode)
    {
        string uri = ExtractUri(paramsNode);
        PublishDiagnostics(uri, []);
    }

    private void HandleShutdown(JsonNode? idCopy, JsonNode? paramsNode)
    {
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

    private static string ExtractUri(JsonNode? paramsNode)
    {
        return (string)paramsNode!["textDocument"]!["uri"]!;
    }

    private static Data RunFrontend(string code)
    {
        Diagnostic diag = new();
        TypeRegistry reg = new();
        Frontend.Frontend.Result result = Frontend.Frontend.Run(code, reg, diag, timers: null);
        List<int> lineOffsets = CalcLineOffsets(code);
        return new Data
        {
            Code = code,
            LineOffsets = lineOffsets,
            Tokens = result.Tokens,
            CompilationUnit = result.CompilationUnit,
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