using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Server.Bootstrap;

namespace ZdoRpgAi.Server.Http;

public class HttpServer {
    private static readonly ILog Log = Logger.Get<HttpServer>();

    private readonly WebApplication _app;
    private readonly int _maxMessageSize;
    private readonly int _rpcTimeoutMs;
    private readonly string _clientToken;
    private readonly List<IChannel> _activeChannels = new();

    public event Action<IChannel>? ClientConnected;

    public HttpServer(HttpServerSection config) {
        _maxMessageSize = config.MaxMessageSize;
        _rpcTimeoutMs = config.RpcTimeoutMs;
        _clientToken = config.ClientToken;

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{config.Host}:{config.Port}");
        _app = builder.Build();

        _app.UseWebSockets();

        _app.Map("/ping", context => {
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("pong");
        });

        _app.Map("/metrics", context => {
            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            return context.Response.WriteAsync(ServerMetrics.RenderPrometheusText(GetActiveChannelCount()));
        });

        _app.Map("/ws", async context => {
            if (!context.WebSockets.IsWebSocketRequest) {
                ServerMetrics.RecordWebSocketRejected(WebSocketRejectionReason.NotWebSocket);
                context.Response.StatusCode = 400;
                return;
            }

            if (_clientToken.Length > 0 && context.Request.Headers["X-ZdoRpgAi-Client"] != _clientToken) {
                ServerMetrics.RecordWebSocketRejected(WebSocketRejectionReason.BadToken);
                context.Response.StatusCode = 403;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var channel = new WebSocketChannel(socket, _maxMessageSize);

            lock (_activeChannels) {
                _activeChannels.Add(channel);
            }

            ServerMetrics.RecordWebSocketAccepted();

            try {
                Log.Info("Client connected");
                ClientConnected?.Invoke(channel);
                await channel.RunWebSocketAsync();
            }
            finally {
                lock (_activeChannels) {
                    _activeChannels.Remove(channel);
                }

                Log.Info("Client disconnected");
            }
        });
    }

    public async Task StartAsync(CancellationToken ct = default) {
        ct.Register(() => {
            lock (_activeChannels) {
                foreach (var channel in _activeChannels) {
                    channel.Close();
                }
            }
        });
        await _app.RunAsync(ct);
    }

    private int GetActiveChannelCount() {
        lock (_activeChannels) {
            return _activeChannels.Count;
        }
    }
}
