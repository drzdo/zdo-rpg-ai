using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Client.VoiceCapture;

namespace ZdoRpgAi.Client.App;

public class ClientApplication : IDisposable {
    private static readonly ILog Log = Logger.Get<ClientApplication>();

    private readonly ClientChannelBridge _bridge;
    private readonly Mp3Manager _mp3;
    private readonly VoiceCaptureService? _voiceCapture;
    private readonly bool _stripDiacritics;

    // Tracked state from mod
    private volatile string? _localPlayerId;
    private volatile string? _lastTargetNpcId;

    public ClientApplication(
        ClientChannelBridge bridge,
        Mp3Manager mp3,
        VoiceCaptureService? voiceCapture = null,
        bool stripDiacritics = false
    ) {
        _bridge = bridge;
        _mp3 = mp3;
        _voiceCapture = voiceCapture;
        _stripDiacritics = stripDiacritics;

        _bridge.ServerMessageReceived += HandleServerMessage;
        _bridge.ModMessageReceived += HandleModMessage;

        if (_voiceCapture != null) {
            _voiceCapture.Activated += HandleMicActivated;
            _voiceCapture.Deactivated += HandleMicDeactivated;
            _voiceCapture.OnAudioBufferAsync = HandleMicAudioBufferAsync;
        }
    }

    public async Task RunAsync(CancellationToken ct) {
        var tasks = new List<Task> {
            _bridge.RunAsync(ct)
        };

        if (_voiceCapture != null) {
            tasks.Add(_voiceCapture.RunAsync(ct));
        }

        await Task.WhenAll(tasks);
    }

    private void HandleServerMessage(Message msg) {
        Log.Debug("Server -> Client: {Type}", msg.Type);

        switch (msg.Type) {
            case nameof(ServerToClientMessageType.NpcSpeaksMp3):
                HandleNpcSpeaksMp3(msg);
                break;
            case nameof(ServerToModMessageType.SpeechRecognitionInProgress): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SpeechRecognitionInProgressPayload);
                    if (p != null)
                        Log.Debug("Speech recognition interim: '{Text}'", p.Text);
                    _bridge.SendMessageToMod(msg);
                    break;
                }
            case nameof(ServerToModMessageType.SpeechRecognitionComplete): {
                    var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SpeechRecognitionCompletePayload);
                    if (p != null)
                        Log.Info("Speech recognition final: '{Text}'", p.Text);
                    _bridge.SendMessageToMod(msg);
                    break;
                }
            default:
                _bridge.SendMessageToMod(msg);
                break;
        }
    }

    private void HandleNpcSpeaksMp3(Message msg) {
        if (msg.Binary == null || msg.Json == null) return;

        var payload = msg.Json.DeserializeSafe(PayloadJsonContext.Default.NpcSpeaksMp3Payload);
        if (payload == null) return;

        var mp3Name = _mp3.SaveMp3(msg.Binary);
        Log.Info("NPC {NpcId} speaks: '{Text}' (audio: {Mp3Name}, duration: {Duration:F1}s)",
            payload.NpcId, payload.Text, mp3Name, payload.DurationSec);

        var text = _stripDiacritics ? DiacriticsStripper.Strip(payload.Text) : payload.Text;
        var sayPayload = new SayMp3FilePayload(payload.NpcId, mp3Name, text, payload.DurationSec);
        var data = JsonExtensions.SerializeToObject(sayPayload, PayloadJsonContext.Default.SayMp3FilePayload);
        _bridge.SendMessageToMod(new Message(nameof(ClientToModMessageType.SayMp3File), 0, null, data, null));
    }

    private void HandleModMessage(Message msg) {
        Log.Debug("Mod -> Client: {Type}", msg.Type);

        switch (msg.Type) {
            case nameof(ModToServerMessageType.PlayerAdded): {
                    var e = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlayerAddedPayload);
                    if (e != null) {
                        _localPlayerId = e.PlayerId;
                        Log.Info("Player ID: {PlayerId}", _localPlayerId);
                    }
                    break;
                }
            case nameof(ModToServerMessageType.TargetChanged): {
                    var e = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.TargetChangedPayload);
                    if (e != null) {
                        _lastTargetNpcId = e.NpcId;
                        if (_lastTargetNpcId != null)
                            Log.Debug("Target NPC: {NpcId}", _lastTargetNpcId);
                    }
                    break;
                }
            case nameof(ModToServerMessageType.CellChange): {
                    var e = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.CellChangePayload);
                    if (e != null)
                        Log.Info("Cell: {CellName}", e.CellName);
                    break;
                }
        }

        _bridge.SendMessageToServer(msg);
    }

    private void HandleMicActivated() {
        var payload = new PlayerStartSpeakPayload(
            _localPlayerId ?? "player",
            _lastTargetNpcId,
            "0"
        );
        var data = JsonExtensions.SerializeToObject(payload, PayloadJsonContext.Default.PlayerStartSpeakPayload);
        _bridge.SendMessageToServer(new Message(nameof(ClientToBothMessageType.PlayerStartSpeak), 0, null, data, null));
        _bridge.SendMessageToMod(new Message(nameof(ClientToBothMessageType.PlayerStartSpeak), 0, null, null, null));
        Log.Info("Mic activated -> PlayerStartSpeak (target={Target})", _lastTargetNpcId ?? "(none)");
    }

    private void HandleMicDeactivated() {
        var payload = new PlayerStopSpeakPayload(_localPlayerId ?? "player");
        var data = JsonExtensions.SerializeToObject(payload, PayloadJsonContext.Default.PlayerStopSpeakPayload);
        _bridge.SendMessageToServer(new Message(nameof(ClientToBothMessageType.PlayerStopSpeak), 0, null, data, null));
        _bridge.SendMessageToMod(new Message(nameof(ClientToBothMessageType.PlayerStopSpeak), 0, null, null, null));
        Log.Info("Mic deactivated -> PlayerStopSpeak");
    }

    private Task HandleMicAudioBufferAsync(byte[] pcmBytes) {
        if (!_bridge.IsServerConnected) return Task.CompletedTask;
        var payload = new PlayerSpeaksAudioPayload(_localPlayerId ?? "player");
        var data = JsonExtensions.SerializeToObject(payload, PayloadJsonContext.Default.PlayerSpeaksAudioPayload);
        _bridge.SendMessageToServer(new Message(nameof(ClientToServerMessageType.PlayerSpeaksAudio), 0, null, data, pcmBytes));
        return Task.CompletedTask;
    }

    public void Dispose() {
        _voiceCapture?.Dispose();
        _bridge.Dispose();
    }
}
