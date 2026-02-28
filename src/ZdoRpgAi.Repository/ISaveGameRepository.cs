namespace ZdoRpgAi.Repository;

public interface ISaveGameRepository : IDisposable {
    long AddStoryEvent(string gameTime, string realTime, string type, string dataJson);
    void AddStoryEventObservers(long storyEventId, string[] characterIds);
    List<RawStoryEvent> GetActiveStoryEventsForCharacter(string characterId);
    List<RawStoryEventSummary> GetActiveSummariesForCharacter(string characterId);
    long AddStoryEventSummary(string summary, string realTime, string[] characterIds);
    void ArchiveStoryEvents(long[] eventIds, long summaryId);
    void ArchiveStoryEventSummaries(long[] summaryIds, long newSummaryId);
    RawNpcInfo? GetNpcInfo(string npcId);
    void SaveNpcInfo(RawNpcInfo info);
}

public record RawStoryEvent(long Id, string GameTime, string RealTime, string Type, string DataJson);
public record RawStoryEventSummary(long Id, string Summary, string RealTime);
public record RawNpcInfo(string Id, string Name, string Race, string Sex);
