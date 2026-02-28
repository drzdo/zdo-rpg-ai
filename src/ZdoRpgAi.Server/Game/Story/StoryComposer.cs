using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.Server.Game.Story;

public class StoryComposer {
    private static readonly ILog Log = Logger.Get<StoryComposer>();

    private readonly Story _story;
    private IRpcChannel? _client;

    public StoryComposer(Story story) {
        _story = story;
    }

    public void OnClientConnected(IRpcChannel client) {
        _client = client;
        client.MessageReceived += OnMessageReceived;
    }

    public void OnClientDisconnected() {
        _client = null;
    }

    public void OnPlayerSpeak(string playerId, string? targetCharacterId, string gameTime, string text) {
        _ = OnPlayerSpeakAsync(playerId, targetCharacterId, gameTime, text);
    }

    public async Task<string[]> QueryObserverIdsAsync(StoryEvent evt) {
        var (speakerId, targetId) = evt switch {
            StoryEvent.PlayerSpeak ps => (ps.PlayerCharacterId, ps.TargetCharacterId),
            StoryEvent.NpcSpeak ns => (ns.NpcCharacterId, ns.TargetCharacterId),
            _ => ((string?)null, (string?)null),
        };

        if (speakerId == null) return [];
        return await QueryObserverIdsAsync(speakerId, targetId);
    }

    private async Task OnPlayerSpeakAsync(string playerId, string? targetCharacterId, string gameTime, string text) {
        var observerIds = await QueryObserverIdsAsync(playerId, targetCharacterId);

        var evt = StoryEvent.Create(new StoryEvent.PlayerSpeak {
            PlayerCharacterId = playerId,
            TargetCharacterId = targetCharacterId,
            GameTime = gameTime,
            Text = text,
        });
        _story.RegisterEvent(evt, observerIds);
    }

    private async Task<string[]> QueryObserverIdsAsync(string speakerId, string? targetId) {
        var client = _client;
        if (client == null) return [];

        try {
            var response = await client.CallAsync(
                nameof(ServerToModMessageType.GetCharactersWhoHear),
                JsonExtensions.SerializeToObject(
                    new GetCharactersWhoHearRequestPayload(speakerId),
                    PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload));

            var payload = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload);
            var characters = payload?.Characters ?? [];

            return characters
                .Select(c => c.CharacterId)
                .Where(id => id != speakerId && id != targetId)
                .ToArray();
        }
        catch (Exception ex) {
            Log.Warn("Failed to query observers: {Error}", ex.Message);
            return [];
        }
    }

    private void OnMessageReceived(Message msg) {
        switch (msg.Type) {
            case nameof(ClientToServerMessageType.PlayerSpeaksText): {
                    var payload = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlayerSpeaksTextPayload);
                    if (payload == null) {
                        return;
                    }

                    OnPlayerSpeak(payload.PlayerId, payload.TargetCharacterId, payload.GameTime, payload.Text);
                    break;
                }
        }
    }
}
