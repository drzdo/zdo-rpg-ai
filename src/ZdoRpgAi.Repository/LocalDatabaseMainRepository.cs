using System.Text.Json;
using ZdoRpgAi.Database;

namespace ZdoRpgAi.Repository;

public class LocalDatabaseMainRepository : IMainRepository, IDisposable {
    private readonly MainDatabase _db;

    public LocalDatabaseMainRepository(string path) {
        _db = new MainDatabase(path);
        _db.Open();
    }

    public RawNpcInfo? GetNpcInfo(string npcId) {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT dataJson FROM npc WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", npcId);
        var json = cmd.ExecuteScalar() as string;
        if (json == null) return null;
        var data = JsonSerializer.Deserialize(json, NpcDataJsonContext.Default.NpcDataJson);
        if (data == null) return null;
        return new RawNpcInfo(npcId, data.Name, data.Race, data.Sex);
    }

    public void Dispose() {
        _db.Dispose();
    }
}
