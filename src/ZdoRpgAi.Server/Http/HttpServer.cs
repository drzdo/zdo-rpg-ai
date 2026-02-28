using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Bootstrap;

namespace ZdoRpgAi.Server.Http;

public class HttpServer {
    private static readonly ILog Log = Logger.Get<HttpServer>();

    private readonly WebApplication _app;
    private readonly int _maxMessageSize;
    private readonly int _rpcTimeoutMs;
    private readonly string _clientToken;

    public event Action<RpcChannel>? ClientConnected;

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

        _app.Map("/ws", async context => {
            if (!context.WebSockets.IsWebSocketRequest) {
                context.Response.StatusCode = 400;
                return;
            }

            if (_clientToken.Length > 0 && context.Request.Headers["X-ZdoRpgAi-Client"] != _clientToken) {
                context.Response.StatusCode = 403;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var channel = new WebSocketChannel(socket, _maxMessageSize);
            var rpc = new RpcChannel(channel, _rpcTimeoutMs);

            Log.Info("Client connected");
            ClientConnected?.Invoke(rpc);

            await rpc.RunAsync();
            Log.Info("Client disconnected");
        });
    }

    public async Task StartAsync(CancellationToken ct = default) {
        await _app.RunAsync(ct);
    }
}
