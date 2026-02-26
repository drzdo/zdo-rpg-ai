using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;

namespace ZdoRpgAi.Protocol.Rpc;

public class RpcChannel {
    private static readonly ILog Log = Logger.Get<RpcChannel>();

    private readonly IChannel _channel;
    private readonly int _callTimeoutMs;
    private int _nextId;
    private readonly Dictionary<int, TaskCompletionSource<Message>> _pending = new();

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    public RpcChannel(IChannel channel, int callTimeoutMs = 5000) {
        _channel = channel;
        _callTimeoutMs = callTimeoutMs;
        _channel.MessageReceived += OnChannelMessage;
        _channel.Disconnected += OnChannelDisconnected;
    }

    public void Publish(string type, JsonObject? payload = null, byte[]? binary = null) {
        var id = Interlocked.Increment(ref _nextId);
        var msg = new Message(type, id, null, payload, binary);
        _channel.SendMessage(msg);
    }

    public void Respond(string type, int responseTo, JsonObject? payload = null) {
        var id = Interlocked.Increment(ref _nextId);
        var msg = new Message(type, id, responseTo, payload, null);
        _channel.SendMessage(msg);
    }

    public async Task<Message> CallAsync(string type, JsonObject? payload = null, int? timeoutMs = null) {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<Message>();

        lock (_pending) {
            _pending[id] = tcs;
        }

        var msg = new Message(type, id, null, payload, null);
        _channel.SendMessage(msg);

        var timeout = timeoutMs ?? _callTimeoutMs;
        using var cts = new CancellationTokenSource(timeout);

        try {
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            lock (_pending) {
                _pending.Remove(id);
            }
            throw new TimeoutException($"No response for message {id} (type={type})");
        }
    }

    public Task RunAsync() => _channel.RunAsync();

    public void Close() => _channel.Close();

    private void OnChannelMessage(Message msg) {
        if (msg.ResponseTo.HasValue) {
            Log.Trace("RECV response (to={ResponseTo}): {Type}", msg.ResponseTo.Value, msg.Type);
            TaskCompletionSource<Message>? tcs;
            lock (_pending) {
                _pending.Remove(msg.ResponseTo.Value, out tcs);
            }
            tcs?.TrySetResult(msg);
            return;
        }

        MessageReceived?.Invoke(msg);
    }

    private void OnChannelDisconnected() {
        lock (_pending) {
            foreach (var tcs in _pending.Values) {
                tcs.TrySetCanceled();
            }
            _pending.Clear();
        }
        Disconnected?.Invoke();
    }
}
