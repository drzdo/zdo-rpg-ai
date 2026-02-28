using System.Text.Json;
using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;

namespace ZdoRpgAi.Server.Game.Story;

public class Story : IStory {
    private static readonly ILog Log = Logger.Get<Story>();

    private readonly ISaveGameRepository _saveGameRepo;
    private readonly StorySummaryBuilder _summaryBuilder;
    private readonly DirectorSection _config;

    public Story(ISaveGameRepository saveGameRepo, StorySummaryBuilder summaryBuilder, DirectorSection config) {
        _saveGameRepo = saveGameRepo;
        _summaryBuilder = summaryBuilder;
        _config = config;
    }

    public event Action<StoryEvent>? EventRegistered;

    public StoryEvent RegisterEvent(StoryEvent evt, string[] observerCharacterIds) {
        var type = evt.GetType().Name;
        var dataJson = JsonSerializer.Serialize(evt, StoryEventJsonContext.Default.StoryEvent);
        var id = _saveGameRepo.AddStoryEvent(evt.GameTime, evt.RealTime, type, dataJson);
        _saveGameRepo.AddStoryEventObservers(id, observerCharacterIds);
        Log.Info("Registered story event #{Id}: {Type} ({ObserverCount} observers)", id, type, observerCharacterIds.Length);
        var registered = evt with { Id = id };
        EventRegistered?.Invoke(registered);
        return registered;
    }

    public async Task<(List<StoryEvent> Events, List<StoryEventSummary> Summaries)> GetHistoryForCharacterAsync(string characterId) {
        var rawEvents = _saveGameRepo.GetActiveStoryEventsForCharacter(characterId);
        var events = rawEvents.Select(DeserializeEvent).Where(e => e != null).Select(e => e!).ToList();
        var rawSummaries = _saveGameRepo.GetActiveSummariesForCharacter(characterId);
        var summaries = rawSummaries.Select(s => new StoryEventSummary(s.Id, s.Summary, s.RealTime)).ToList();

        if (events.Count > _config.CompactThreshold) {
            var toCompact = events[..^_config.CompactKeepRecent];
            events = events[^_config.CompactKeepRecent..];

            var participantIds = CollectParticipantIds(toCompact);
            var summaryText = await _summaryBuilder.SummarizeEventsAsync(toCompact);
            var realTime = StoryEvent.GetRealTime();
            var summaryId = _saveGameRepo.AddStoryEventSummary(summaryText, realTime, participantIds);
            _saveGameRepo.ArchiveStoryEvents(toCompact.Select(e => e.Id).ToArray(), summaryId);

            Log.Info("Compacted {Count} events into summary #{SummaryId} for {CharacterId}", toCompact.Count, summaryId, characterId);

            rawSummaries = _saveGameRepo.GetActiveSummariesForCharacter(characterId);
            summaries = rawSummaries.Select(s => new StoryEventSummary(s.Id, s.Summary, s.RealTime)).ToList();
        }

        if (summaries.Count > _config.CompactThreshold) {
            var toCompact = summaries[..^_config.CompactKeepRecent];
            summaries = summaries[^_config.CompactKeepRecent..];

            var summaryText = await _summaryBuilder.ConsolidateSummariesAsync(toCompact);
            var realTime = StoryEvent.GetRealTime();
            var participantIds = new[] { characterId };
            var newSummaryId = _saveGameRepo.AddStoryEventSummary(summaryText, realTime, participantIds);
            _saveGameRepo.ArchiveStoryEventSummaries(toCompact.Select(s => s.Id).ToArray(), newSummaryId);

            Log.Info("Consolidated {Count} summaries into summary #{SummaryId} for {CharacterId}", toCompact.Count, newSummaryId, characterId);

            rawSummaries = _saveGameRepo.GetActiveSummariesForCharacter(characterId);
            summaries = rawSummaries.Select(s => new StoryEventSummary(s.Id, s.Summary, s.RealTime)).ToList();
        }

        return (events, summaries);
    }

    private static string[] CollectParticipantIds(List<StoryEvent> events) {
        var ids = new HashSet<string>();
        foreach (var evt in events) {
            if (evt is StoryEvent.PlayerSpeak ps) {
                ids.Add(ps.PlayerCharacterId);
                if (ps.TargetCharacterId != null) ids.Add(ps.TargetCharacterId);
            }
            else if (evt is StoryEvent.NpcSpeak ns) {
                ids.Add(ns.NpcCharacterId);
                if (ns.TargetCharacterId != null) ids.Add(ns.TargetCharacterId);
            }
        }
        return ids.ToArray();
    }

    private static StoryEvent? DeserializeEvent(RawStoryEvent raw) {
        try {
            var evt = JsonSerializer.Deserialize(raw.DataJson, StoryEventJsonContext.Default.StoryEvent);
            return evt is null ? null : evt with { Id = raw.Id };
        }
        catch (JsonException ex) {
            Log.Warn("Failed to deserialize story event #{Id}: {Error}", raw.Id, ex.Message);
            return null;
        }
    }
}
