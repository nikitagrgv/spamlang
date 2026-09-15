using System.Text;
using System.Text.Json.Nodes;

namespace Spamlang.Lsp;

public class Server
{
    private Stream _input;
    private Stream _output;
    private bool _exit;

    private const int InternalErrorCode = -32603;
    private const int MethodNotFoundCode = -32601;

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
                JsonObject reply = new()
                {
                    ["capabilities"] = new JsonObject { ["textDocumentSync"] = 1 },
                    ["serverInfo"] = new JsonObject { ["name"] = "spamlang" },
                };
                Reply(idCopy, reply);
                break;
            case "textDocument/didOpen":
            case "textDocument/didChange":
            case "textDocument/didClose":
            case "shutdown":
                Reply(idCopy, null);
                break;
            case "exit":
                _exit = true;
                break;
            default:
                if (idCopy != null && method != null)
                {
                    ReplyError(idCopy.DeepClone(), MethodNotFoundCode, $"Method not found: {method}");
                }

                break;
        }
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
}