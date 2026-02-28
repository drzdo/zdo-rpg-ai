using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Repository.Data;
using ZdoRpgAi.Server.SpeechToText;

namespace ZdoRpgAi.Server.Game;

public class PlayerMessageHandler {
    private static readonly ILog Log = Logger.Get<PlayerMessageHandler>();

    private readonly ISaveGameRepository _saveGameRepo;
    private readonly ISpeechToText _stt;
    private IRpcChannel? _client;
    private SpeakingSession? _activeSession;

    public PlayerMessageHandler(ISaveGameRepository saveGameRepo, ISpeechToText stt) {
        _saveGameRepo = saveGameRepo;
        _stt = stt;
    }

    public void OnClientConnected(IRpcChannel client) {
        _client = client;
        client.MessageReceived += OnMessageReceived;
        _stt.InterimResultReceived += OnInterimResultReceived;
        _stt.FinalResultReceived += OnFinalResultReceived;
    }

    public void OnClientDisconnected() {
        _stt.InterimResultReceived -= OnInterimResultReceived;
        _stt.FinalResultReceived -= OnFinalResultReceived;

        if (_activeSession != null) {
            _stt.Cancel();
            _activeSession = null;
        }

        _client = null;
    }

    private void OnMessageReceived(Message msg) {
        switch (msg.Type) {
            case nameof(ClientToServerMessageType.PlayerSpeaksText):
                _ = HandlePlayerSpeaksTextAsync(msg);
                break;
            case nameof(ClientToBothMessageType.PlayerStartSpeak):
                HandlePlayerStartSpeak(msg);
                break;
            case nameof(ClientToServerMessageType.PlayerSpeaksAudio):
                HandlePlayerSpeaksAudio(msg);
                break;
            case nameof(ClientToBothMessageType.PlayerStopSpeak):
                HandlePlayerStopSpeak(msg);
                break;
        }
    }

    private void HandlePlayerStartSpeak(Message msg) {
        var payload = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlayerStartSpeakPayload);
        if (payload == null) return;

        if (_activeSession != null) {
            Log.Warn("Player {PlayerId} started speaking while a session is already active, ignoring",
                payload.PlayerId);
            return;
        }

        Log.Info("Player {PlayerId} started speaking", payload.PlayerId);
        _activeSession = new SpeakingSession(payload.PlayerId, payload.TargetCharacterId, payload.GameTime);
        _stt.Start();
    }

    private void HandlePlayerSpeaksAudio(Message msg) {
        if (_activeSession == null) {
            Log.Warn("Received audio without an active speaking session");
            return;
        }
        if (msg.Binary == null) {
            Log.Warn("Received PlayerSpeaksAudio without binary data");
            return;
        }
        _stt.FeedAudio(msg.Binary);
    }

    private void HandlePlayerStopSpeak(Message msg) {
        if (_activeSession == null) {
            Log.Warn("Received PlayerStopSpeak without an active speaking session");
            return;
        }

        var payload = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlayerStopSpeakPayload);
        if (payload == null) return;

        if (payload.Cancel) {
            Log.Info("Player {PlayerId} cancelled speaking", payload.PlayerId);
            _stt.Cancel();
            _activeSession = null;
        }
        else {
            Log.Info("Player {PlayerId} stopped speaking", payload.PlayerId);
            _stt.Finish();
        }
    }

    private void OnInterimResultReceived(string text) {
        var session = _activeSession;
        var client = _client;
        if (session == null || client == null) {
            Log.Warn("Received interim STT result but no active session or client");
            return;
        }

        Log.Debug("Interim recognition for {PlayerId}: {Text}", session.PlayerId, text);
        client.Publish(
            nameof(ServerToModMessageType.SpeechRecognitionInProgress),
            JsonExtensions.SerializeToObject(
                new SpeechRecognitionInProgressPayload(session.PlayerId, text),
                PayloadJsonContext.Default.SpeechRecognitionInProgressPayload));
    }

    private void OnFinalResultReceived(string text) {
        var session = _activeSession;
        _activeSession = null;

        var client = _client;
        if (session == null || client == null) {
            Log.Warn("Received final STT result but no active session or client");
            return;
        }

        Log.Info("Final recognition for {PlayerId}: {Text}", session.PlayerId, text);
        client.Publish(
            nameof(ServerToModMessageType.SpeechRecognitionComplete),
            JsonExtensions.SerializeToObject(
                new SpeechRecognitionCompletePayload(session.PlayerId, text),
                PayloadJsonContext.Default.SpeechRecognitionCompletePayload));

        _ = StoreConversationEntryAsync(client, session, text);
    }

    private async Task StoreConversationEntryAsync(IRpcChannel client, SpeakingSession session, string text) {
        var response = await client.CallAsync(
            nameof(ServerToModMessageType.GetCharactersWhoHear),
            JsonExtensions.SerializeToObject(
                new GetCharactersWhoHearRequestPayload(session.PlayerId),
                PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload));

        var hearResponse = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload);
        var listeners = hearResponse?.Characters ?? [];

        Log.Info("Player {PlayerId} heard by {Count} characters", session.PlayerId, listeners.Length);

        var listenerIds = listeners
            .Select(l => l.CharacterId)
            .Where(id => id != session.PlayerId && id != session.TargetCharacterId)
            .ToArray();

        _saveGameRepo.AddConversationEntry(
            session.PlayerId,
            session.TargetCharacterId,
            session.GameTime,
            ConversationEntryType.Speak,
            new SpeakEntryData(text),
            listenerIds);
    }

    private async Task HandlePlayerSpeaksTextAsync(Message msg) {
        var payload = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlayerSpeaksTextPayload);
        if (payload == null) return;

        var client = _client;
        if (client == null) {
            Log.Warn("Received PlayerSpeaksText but no client connected");
            return;
        }

        Log.Info("Player {PlayerId} speaks: {Text}", payload.PlayerId, payload.Text);

        var session = new SpeakingSession(payload.PlayerId, payload.TargetCharacterId, payload.GameTime);
        await StoreConversationEntryAsync(client, session, payload.Text);
    }

    private record SpeakingSession(string PlayerId, string? TargetCharacterId, string GameTime);
}
