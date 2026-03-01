using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Game.Director;

namespace ZdoRpgAi.Server.Game.Story;

public class StoryComposer {
    private static readonly ILog Log = Logger.Get<StoryComposer>();

    private readonly Story _story;
    private readonly DirectorHelper _directorHelper;

    public StoryComposer(Story story, DirectorHelper directorHelper, IRpcChannel rpc) {
        _story = story;
        _directorHelper = directorHelper;
        rpc.MessageReceived += OnMessageReceived;
    }

    public void OnPlayerSpeak(string playerId, string? targetCharacterId, string gameTime, string text) {
        Log.Debug("OnPlayerSpeak");
        _ = OnPlayerSpeakAsync(playerId, targetCharacterId, gameTime, text);
    }

    private async Task OnPlayerSpeakAsync(string playerId, string? targetCharacterId, string gameTime, string text) {
        try {
            Log.Trace("Querying observers for player {PlayerId}", playerId);
            var observerIds = await _directorHelper.QueryObserverIdsAsync(playerId, targetCharacterId != null ? [targetCharacterId] : null);
            Log.Trace("Got {Count} observers, registering event", observerIds.Length);

            var evt = StoryEvent.Create(new StoryEvent.PlayerSpeak {
                PlayerCharacterId = playerId,
                TargetCharacterId = targetCharacterId,
                GameTime = gameTime,
                Text = text,
            });
            _story.RegisterEvent(evt, observerIds);
        }
        catch (Exception ex) {
            Log.Error("OnPlayerSpeakAsync failed: {Error}", ex.Message);
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
