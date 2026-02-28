using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Server.Game;

public record NpcInfo(string Id, string Name, string Race, string Sex);

public class NpcRepository {
    private static readonly ILog Log = Logger.Get<NpcRepository>();

    private readonly IMainRepository _mainRepo;
    private readonly ISaveGameRepository _saveGameRepo;
    private IRpcChannel? _client;

    public NpcRepository(IMainRepository mainRepo, ISaveGameRepository saveGameRepo) {
        _mainRepo = mainRepo;
        _saveGameRepo = saveGameRepo;
    }

    public void SetClient(IRpcChannel? client) {
        _client = client;
    }

    public async Task<NpcInfo?> GetNpcInfoAsync(string npcId) {
        var raw = _saveGameRepo.GetNpcInfo(npcId)
               ?? _mainRepo.GetNpcInfo(npcId);
        if (raw != null) return ToNpcInfo(raw);

        var client = _client;
        if (client == null) {
            Log.Warn("NPC {NpcId} not found in repos and no client connected", npcId);
            return null;
        }

        var response = await client.CallAsync(
            nameof(ServerToModMessageType.GetNpcInfo),
            JsonExtensions.SerializeToObject(
                new GetNpcInfoRequestPayload(npcId),
                PayloadJsonContext.Default.GetNpcInfoRequestPayload));

        var payload = response.Json?.DeserializeSafe(PayloadJsonContext.Default.GetNpcInfoResponsePayload);
        if (payload == null) {
            Log.Warn("Mod returned no info for NPC {NpcId}", npcId);
            return null;
        }

        var info = new RawNpcInfo(npcId, payload.Name, payload.Race, payload.Sex);
        _saveGameRepo.SaveNpcInfo(info);
        return ToNpcInfo(info);
    }

    private static NpcInfo ToNpcInfo(RawNpcInfo raw) => new(raw.Id, raw.Name, raw.Race, raw.Sex);
}
