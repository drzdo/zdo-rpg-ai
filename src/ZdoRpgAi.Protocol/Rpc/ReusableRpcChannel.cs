using System.Text.Json.Nodes;
using ZdoRpgAi.Protocol.Channel;

namespace ZdoRpgAi.Protocol.Rpc;

public class ReusableRpcChannel : IRpcChannel {
    private IRpcChannel? _underlying;

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    public IRpcChannel? Underlying {
        get => _underlying;
        set {
            var previous = _underlying;
            if (previous != null) {
                previous.MessageReceived -= OnUnderlyingMessageReceived;
                previous.Disconnected -= OnUnderlyingDisconnected;
            }

            _underlying = value;

            if (value != null) {
                value.MessageReceived += OnUnderlyingMessageReceived;
                value.Disconnected += OnUnderlyingDisconnected;
            }
        }
    }

    public int Publish(string type, JsonObject? payload = null, byte[]? binary = null) {
        return GetUnderlying().Publish(type, payload, binary);
    }

    public int Respond(string type, int responseTo, JsonObject? payload = null) {
        return GetUnderlying().Respond(type, responseTo, payload);
    }

    public Task<Message> CallAsync(string type, JsonObject? payload = null, int? timeoutMs = null) {
        return GetUnderlying().CallAsync(type, payload, timeoutMs);
    }

    private IRpcChannel GetUnderlying() {
        return _underlying ?? throw new InvalidOperationException("Underlying RPC channel is not set");
    }

    private void OnUnderlyingMessageReceived(Message msg) {
        MessageReceived?.Invoke(msg);
    }

    private void OnUnderlyingDisconnected() {
        Disconnected?.Invoke();
    }

    public Task RunAsync() {
        return Task.CompletedTask;
    }
}
