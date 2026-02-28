namespace ZdoRpgAi.Server.Game.Story;

public interface IStory {
    Task<(List<StoryEvent> Events, List<StoryEventSummary> Summaries)> GetHistoryForCharacterAsync(string characterId);
}
