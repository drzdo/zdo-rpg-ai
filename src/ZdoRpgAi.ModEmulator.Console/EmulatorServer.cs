using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;

namespace ZdoRpgAi.ModEmulator.Console;

public class EmulatorServer {
    private static readonly ILog Log = Logger.Get<EmulatorServer>();

    private readonly WebApplication _app;
    private readonly int _maxMessageSize = 10 * 1024 * 1024;

    private EmulatorSession? _session;

    public EmulatorSession? Session => _session;

    public EmulatorServer(string host, int port) {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{host}:{port}");
        _app = builder.Build();

        _app.UseWebSockets();

        _app.Map("/ws", async context => {
            if (!context.WebSockets.IsWebSocketRequest) {
                context.Response.StatusCode = 400;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var channel = new WebSocketChannel(socket, _maxMessageSize);

            Log.Info("Client connected");

            var session = new EmulatorSession(channel);
            _session = session;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            await Task.WhenAll(
                session.RunAsync(cts.Token),
                channel.RunWebSocketAsync()
            );

            if (_session == session) {
                _session = null;
            }

            Log.Info("Client disconnected");
        });
    }

    public async Task RunAsync(CancellationToken ct) {
        await _app.RunAsync(ct);
    }
}
