using System.Net.WebSockets;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;

namespace ZdoRpgAi.Client.Channel;

public class EmulatorModChannel : IChannel {
    private static readonly ILog Log = Logger.Get<EmulatorModChannel>();

    private readonly string _uri;
    private readonly CancellationTokenSource _cts = new();

    private WebSocketChannel? _inner;

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    public EmulatorModChannel(string host, int port) {
        _uri = $"ws://{host}:{port}/ws";
    }

    public void SendMessage(Message msg) {
        if (_inner == null) {
            Log.Warn("Emulator not connected, dropping: {Type}", msg.Type);
            return;
        }

        _inner.SendMessage(msg);
    }

    public async Task RunAsync() {
        try {
            Log.Info("Connecting to mod emulator at {Uri}", _uri);
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_uri), _cts.Token);
            Log.Info("Connected to mod emulator");

            _inner = new WebSocketChannel(ws, 10 * 1024 * 1024);
            _inner.MessageReceived += msg => MessageReceived?.Invoke(msg);
            _inner.Disconnected += () => {
                _inner = null;
                Disconnected?.Invoke();
            };

            await _inner.RunWebSocketAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) {
            Log.Error("Emulator connection error: {Error}", ex.Message);
        }
        finally {
            _inner = null;
            Disconnected?.Invoke();
        }
    }

    public void Close() {
        _cts.Cancel();
        _inner?.Close();
    }
}
