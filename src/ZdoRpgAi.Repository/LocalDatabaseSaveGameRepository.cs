using System.Text.Json;
using System.Text.Json.Serialization;
using ZdoRpgAi.Database;
using ZdoRpgAi.Repository.Data;

namespace ZdoRpgAi.Repository;

public class LocalDatabaseSaveGameRepository : ISaveGameRepository, IDisposable {
    private readonly SaveGameDatabase _db;

    public LocalDatabaseSaveGameRepository(string path) {
        _db = new SaveGameDatabase(path);
        _db.Open();
    }

    public long AddConversationEntry(string speakerCharacterId, string? targetCharacterId,
        string createdAtGameTime, ConversationEntryType type, object data, string[] listenerCharacterIds) {
        var json = SerializeData(data);

        using var tx = _db.Connection.BeginTransaction();

        using var insertEntry = _db.Connection.CreateCommand();
        insertEntry.CommandText = """
            INSERT INTO conversation_entry (speakerCharacterId, targetCharacterId, createdAtGameTime, createdAtRealTime, type, dataJson)
            VALUES ($speaker, $target, $gameTime, datetime('now'), $type, $data)
            RETURNING id
            """;
        insertEntry.Parameters.AddWithValue("$speaker", speakerCharacterId);
        insertEntry.Parameters.AddWithValue("$target", (object?)targetCharacterId ?? DBNull.Value);
        insertEntry.Parameters.AddWithValue("$gameTime", createdAtGameTime);
        insertEntry.Parameters.AddWithValue("$type", type.ToString());
        insertEntry.Parameters.AddWithValue("$data", json);
        var entryId = (long)insertEntry.ExecuteScalar()!;

        foreach (var listenerId in listenerCharacterIds) {
            using var insertListener = _db.Connection.CreateCommand();
            insertListener.CommandText = "INSERT INTO conversation_entry_listener (entryId, listenerCharacterId) VALUES ($entryId, $listener)";
            insertListener.Parameters.AddWithValue("$entryId", entryId);
            insertListener.Parameters.AddWithValue("$listener", listenerId);
            insertListener.ExecuteNonQuery();
        }

        tx.Commit();
        return entryId;
    }

    public long AddStoryEvent(string gameTime, string realTime, string type, string dataJson) {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO story_event (gameTime, realTime, type, dataJson)
            VALUES ($gameTime, $realTime, $type, $dataJson)
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("$gameTime", gameTime);
        cmd.Parameters.AddWithValue("$realTime", realTime);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$dataJson", dataJson);
        return (long)cmd.ExecuteScalar()!;
    }

    public void Dispose() {
        _db.Dispose();
    }

    private static string SerializeData(object data) => data switch {
        SpeakEntryData speak => JsonSerializer.Serialize(speak, RepositoryJsonContext.Default.SpeakEntryData),
        _ => throw new ArgumentException($"Unknown conversation entry data type: {data.GetType().Name}")
    };
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SpeakEntryData))]
internal partial class RepositoryJsonContext : JsonSerializerContext;
