using System.Text;
using System.Text.Json.Nodes;

namespace Spamlang.Lsp;

public class Server
{
    private Stream _input;
    private Stream _output;
    private bool _exit;

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
                    const int internalErrorCode = -32603;
                    ReplyError(id.DeepClone(), internalErrorCode, e.Message);
                }
            }
        }
    }

    private JsonNode? ReadMessage()
    {
    }

    private void HandleMessage(JsonNode message)
    {
    }

    private void Reply(JsonNode? id, JsonNode? result)
    {
        JsonObject reply = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };
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

    private void Send(JsonNode message)
    {
        byte[] body = Encoding.UTF8.GetBytes(message.ToJsonString());
        _output.Write(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"));
        _output.Write(body);
        _output.Flush();
    }
}