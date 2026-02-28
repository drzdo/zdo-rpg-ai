using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Server.Game.Story;

public class StoryComposer {
    private static readonly ILog Log = Logger.Get<StoryComposer>();

    private readonly Story _story;
    private IRpcChannel? _client;

    public StoryComposer(ISaveGameRepository saveGameRepo) {
        _story = new Story(saveGameRepo);
    }

    public void OnClientConnected(IRpcChannel client) {
        _client = client;
        client.MessageReceived += OnMessageReceived;
    }

    public void OnClientDisconnected() {
        _client = null;
    }

    public void OnPlayerSpeak(string playerId, string? targetCharacterId, string gameTime, string text) {
        var evt = StoryEvent.Create(new StoryEvent.PlayerSpeak {
            PlayerCharacterId = playerId,
            TargetCharacterId = targetCharacterId,
            GameTime = gameTime,
            Text = text,
        });
        _story.RegisterEvent(evt);
    }

    private void OnMessageReceived(Message msg) {
        switch (msg.Type) {
            case nameof(ClientToServerMessageType.PlayerSpeaksText): {
                    var payload = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlayerSpeaksTextPayload);
                    if (payload == null) return;
                    OnPlayerSpeak(payload.PlayerId, payload.TargetCharacterId, payload.GameTime, payload.Text);
                    break;
                }
        }
    }
}
