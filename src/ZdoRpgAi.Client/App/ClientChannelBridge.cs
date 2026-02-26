using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.Client.App;

public class ClientChannelBridge : IDisposable {
    private static readonly ILog Log = Logger.Get<ClientChannelBridge>();

    private readonly ServerConnection _server;
    private readonly RpcChannel _modRpc;
    private RpcChannel? _serverRpc;

    // NAT: modId → originalId (for translating mod responses back to server IDs)
    private readonly Dictionary<int, int> _modIdToOriginalId = new();
    // NAT: serverId → originalId (for translating server responses back to mod IDs)
    private readonly Dictionary<int, int> _serverIdToOriginalId = new();

    public event Action? ConnectedToMod;
    public event Action? ConnectedToServer;
    public event Action? DisconnectedFromMod;
    public event Action? DisconnectedFromServer;
    public event Action<Message>? ModMessageReceived;
    public event Action<Message>? ServerMessageReceived;

    public bool IsServerConnected => _serverRpc != null;

    public ClientChannelBridge(ServerConnection server, RpcChannel modRpc) {
        _server = server;
        _modRpc = modRpc;
    }

    public async Task RunAsync(CancellationToken ct) {
        _server.Connected += HandleServerConnected;
        _server.Disconnected += HandleServerDisconnected;
        _modRpc.MessageReceived += HandleModMessage;
        _modRpc.Disconnected += HandleModDisconnected;

        using var modReg = ct.Register(() => _modRpc.Close());

        await Task.WhenAll(
            _server.RunAsync(ct),
            _modRpc.RunAsync()
        );
    }

    public void SendMessageToMod(Message msg) {
        if (msg.ResponseTo.HasValue) {
            if (!_serverIdToOriginalId.Remove(msg.ResponseTo.Value, out var originalModId)) {
                Log.Warn("NAT miss sending response to mod (responseTo={ResponseTo}, type={Type})",
                    msg.ResponseTo.Value, msg.Type);
                return;
            }
            _modRpc.Respond(msg.Type, originalModId, msg.Json?.DeepClone()?.AsObject());
        }
        else {
            var modId = _modRpc.Publish(msg.Type, msg.Json?.DeepClone()?.AsObject(), msg.Binary);
            if (msg.Id > 0) {
                _modIdToOriginalId[modId] = msg.Id;
                TrimNat(_modIdToOriginalId);
            }
        }
    }

    public void SendMessageToServer(Message msg) {
        if (_serverRpc == null) {
            Log.Debug("Server not connected, dropping: {Type}", msg.Type);
            return;
        }

        if (msg.ResponseTo.HasValue) {
            if (!_modIdToOriginalId.Remove(msg.ResponseTo.Value, out var originalServerId)) {
                Log.Warn("NAT miss sending response to server (responseTo={ResponseTo}, type={Type})",
                    msg.ResponseTo.Value, msg.Type);
                return;
            }
            _serverRpc.Respond(msg.Type, originalServerId, msg.Json);
        }
        else {
            var serverId = _serverRpc.Publish(msg.Type, msg.Json, msg.Binary);
            if (msg.Id > 0) {
                _serverIdToOriginalId[serverId] = msg.Id;
                TrimNat(_serverIdToOriginalId);
            }
        }
    }

    private void HandleServerConnected(RpcChannel rpc) {
        _serverRpc = rpc;
        rpc.MessageReceived += msg => ServerMessageReceived?.Invoke(msg);
        Log.Info("Server connection established");
        ConnectedToServer?.Invoke();
    }

    private void HandleServerDisconnected() {
        _serverRpc = null;
        _serverIdToOriginalId.Clear();
        Log.Info("Server connection lost");
        DisconnectedFromServer?.Invoke();
    }

    private void HandleModMessage(Message msg) {
        ModMessageReceived?.Invoke(msg);
    }

    private void HandleModDisconnected() {
        _modIdToOriginalId.Clear();
        Log.Info("Mod disconnected");
        DisconnectedFromMod?.Invoke();
    }

    private static void TrimNat(Dictionary<int, int> nat) {
        if (nat.Count <= 1000) return;
        var oldest = nat.Keys.Order().Take(nat.Count - 1000).ToList();
        foreach (var key in oldest) {
            nat.Remove(key);
        }
    }

    public void Dispose() {
        _server.Dispose();
        _modRpc.Close();
    }
}
