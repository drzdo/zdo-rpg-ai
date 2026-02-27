using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Server.Lua;

public class LuaContext {
    private readonly IMainRepository _mainRepo;
    private readonly ISaveGameRepository _saveGameRepo;

    public string NpcId { get; }
    public string PlayerId { get; }

    public LuaContext(string npcId, string playerId, IMainRepository mainRepo, ISaveGameRepository saveGameRepo) {
        NpcId = npcId;
        PlayerId = playerId;
        _mainRepo = mainRepo;
        _saveGameRepo = saveGameRepo;
    }
}
