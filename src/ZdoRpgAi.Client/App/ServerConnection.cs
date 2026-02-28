using System.Net.WebSockets;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.Client.App;

public class ServerConnectionConfig {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; }
    public int MaxMessageSize { get; set; } = 10 * 1024 * 1024;
    public int RpcTimeoutMs { get; set; } = 5000;
    public int ReconnectDelayMs { get; set; } = 2000;
    public string ClientToken { get; set; } = "";
}

public class ServerConnection : IDisposable {
    private static readonly ILog Log = Logger.Get<ServerConnection>();

    private readonly string _uri;
    private readonly int _maxMessageSize;
    private readonly int _rpcTimeoutMs;
    private readonly int _reconnectDelayMs;
    private readonly string _clientToken;
    private readonly CancellationTokenSource _cts = new();

    public event Action<RpcChannel>? Connected;
    public event Action? Disconnected;

    public ServerConnection(ServerConnectionConfig config) {
        _uri = $"ws://{config.Host}:{config.Port}/ws";
        _maxMessageSize = config.MaxMessageSize;
        _rpcTimeoutMs = config.RpcTimeoutMs;
        _reconnectDelayMs = config.ReconnectDelayMs;
        _clientToken = config.ClientToken;
    }

    public async Task RunAsync(CancellationToken ct) {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        while (!linked.Token.IsCancellationRequested) {
            try {
                Log.Info("Connecting to server at {Uri}", _uri);
                using var ws = new ClientWebSocket();
                if (_clientToken.Length > 0)
                    ws.Options.SetRequestHeader("X-ZdoRpgAi-Client", _clientToken);
                await ws.ConnectAsync(new Uri(_uri), linked.Token);
                Log.Info("Connected to server");

                var channel = new WebSocketChannel(ws, _maxMessageSize);
                var rpc = new RpcChannel(channel, _rpcTimeoutMs);
                Connected?.Invoke(rpc);

                using var reg = linked.Token.Register(() => rpc.Close());
                await rpc.RunAsync();
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                Log.Warn("Server connection error: {Error}", ex.Message);
            }

            Disconnected?.Invoke();
            Log.Info("Disconnected from server, reconnecting in {Delay}ms", _reconnectDelayMs);

            try {
                await Task.Delay(_reconnectDelayMs, linked.Token);
            }
            catch (OperationCanceledException) {
                break;
            }
        }
    }

    public void Dispose() {
        _cts.Cancel();
        _cts.Dispose();
    }
}
