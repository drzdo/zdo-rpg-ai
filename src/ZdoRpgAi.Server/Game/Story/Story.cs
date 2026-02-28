using System.Text.Json;
using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Server.Game.Story;

public class Story {
    private static readonly ILog Log = Logger.Get<Story>();

    private readonly ISaveGameRepository _saveGameRepo;

    public Story(ISaveGameRepository saveGameRepo) {
        _saveGameRepo = saveGameRepo;
    }

    public StoryEvent RegisterEvent(StoryEvent evt) {
        var type = evt.GetType().Name;
        var dataJson = JsonSerializer.Serialize(evt, StoryEventJsonContext.Default.StoryEvent);
        var id = _saveGameRepo.AddStoryEvent(evt.GameTime, evt.RealTime, type, dataJson);
        Log.Info("Registered story event #{Id}: {Type}", id, type);
        return evt with { Id = id };
    }
}
