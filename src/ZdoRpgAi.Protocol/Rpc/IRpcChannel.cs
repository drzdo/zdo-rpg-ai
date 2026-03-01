using System.Text.Json.Nodes;
using ZdoRpgAi.Protocol.Channel;

namespace ZdoRpgAi.Protocol.Rpc;

public interface IRpcChannel {
    int Publish(string type, JsonObject? payload = null, byte[]? binary = null);
    int Respond(string type, int responseTo, JsonObject? payload = null);
    Task<Message> CallAsync(string type, JsonObject? payload = null, int? timeoutMs = null);
    event Action<Message>? MessageReceived;
    event Action? Disconnected;

    Task RunAsync();
}
