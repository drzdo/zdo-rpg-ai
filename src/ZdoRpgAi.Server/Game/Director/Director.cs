using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Director;

public class Director {
    private static readonly ILog Log = Logger.Get<Director>();

    private readonly Story.Story _story;
    private readonly StoryComposer _storyComposer;
    private readonly SimpleReactiveStrategy _simpleReactive;
    private readonly object _bufferLock = new();
    private readonly List<StoryEvent> _buffer = [];
    private bool _processing;
    private IRpcChannel? _client;

    public Director(Story.Story story, StoryComposer storyComposer, ILlm mainLlm, ILlm simpleLlm, NpcRepository npcRepo) {
        _story = story;
        _storyComposer = storyComposer;
        _simpleReactive = new SimpleReactiveStrategy(mainLlm, simpleLlm, story, npcRepo);
        story.EventRegistered += OnStoryEventRegistered;
    }

    public void SetClient(IRpcChannel? client) {
        _client = client;
        _simpleReactive.SetClient(client);
    }

    private void OnStoryEventRegistered(StoryEvent evt) {
        lock (_bufferLock) {
            _buffer.Add(evt);
            if (_processing) {
                return;
            }

            _processing = true;
        }

        _ = DrainBufferAsync();
    }

    private async Task DrainBufferAsync() {
        while (true) {
            List<StoryEvent> batch;
            lock (_bufferLock) {
                if (_buffer.Count == 0) {
                    _processing = false;
                    return;
                }

                batch = [.. _buffer];
                _buffer.Clear();
            }

            if (batch.Count > 0) {
                await ProcessStoryEventsAsync(batch);
            }
        }
    }

    private async Task ProcessStoryEventsAsync(List<StoryEvent> events) {
        Log.Trace("Received {Count} story events", events.Count);

        var strategy = DetermineStrategy(events);
        if (strategy == null) {
            Log.Trace("No strategy decided");
            return;
        }

        try {
            var newEvents = await strategy.ProcessStoryEventsAsync(events);

            foreach (var evt in newEvents) {
                await RegisterAndPublishAsync(evt);
            }
        }
        catch (Exception ex) {
            Log.Error("Strategy {Strategy} failed: {Error}", strategy.GetType().Name, ex.Message);
        }
    }

    private async Task RegisterAndPublishAsync(StoryEvent evt) {
        var observerIds = await _storyComposer.QueryObserverIdsAsync(evt);
        var registered = _story.RegisterEvent(evt, observerIds);

        var client = _client;
        if (client == null) return;

        if (registered is StoryEvent.NpcSpeak npcSpeak) {
            client.Publish(
                nameof(ServerToModMessageType.NpcSpeaks),
                JsonExtensions.SerializeToObject(
                    new NpcSpeaksPayload(npcSpeak.NpcCharacterId, npcSpeak.Text),
                    PayloadJsonContext.Default.NpcSpeaksPayload));

            Log.Info("NPC {NpcId} responds: {Text}", npcSpeak.NpcCharacterId, npcSpeak.Text);
        }
    }

    private IDirectorStrategy? DetermineStrategy(List<StoryEvent> events) {
        if (events.Last() is StoryEvent.PlayerSpeak) {
            return _simpleReactive;
        }

        return null;
    }
}
