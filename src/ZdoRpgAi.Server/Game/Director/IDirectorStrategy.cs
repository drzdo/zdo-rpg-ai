using ZdoRpgAi.Server.Game.Story;

namespace ZdoRpgAi.Server.Game.Director;

// Strategy should not register events directory to Story.
// It should return new StoryEvents to director, and it is up to director to register them.
public interface IDirectorStrategy {
    Task<List<StoryEvent>> ProcessStoryEventsAsync(List<StoryEvent> events);
}
