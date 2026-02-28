using Microsoft.Data.Sqlite;

namespace ZdoRpgAi.Database.Migrations.SaveGame;

public class SaveGame001 : IMigration {
    public void Before(SqliteConnection conn) {
        conn.Execute("CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
        conn.Execute("INSERT INTO meta (key, value) VALUES ('dbtype', 'savegame')");
    }

    public string GetSql() => """
        CREATE TABLE player (
            id TEXT PRIMARY KEY,
            dataJson TEXT NOT NULL
        );

        CREATE TABLE npc_new (
            id TEXT PRIMARY KEY,
            dataJson TEXT NOT NULL
        );

        CREATE TABLE npc_attribute_value_new (
            npcId TEXT NOT NULL,
            attributeId TEXT NOT NULL,
            dataJson TEXT NOT NULL,
            PRIMARY KEY (npcId, attributeId)
        );

        CREATE TABLE conversation_entry (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            speakerCharacterId TEXT NOT NULL,
            targetCharacterId TEXT,
            createdAtGameTime TEXT NOT NULL,
            createdAtRealTime TEXT NOT NULL,
            type TEXT NOT NULL,
            dataJson TEXT NOT NULL
        );

        CREATE TABLE conversation_entry_listener (
            entryId INTEGER NOT NULL,
            listenerCharacterId TEXT NOT NULL
        );

        CREATE TABLE story_event (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            gameTime TEXT NOT NULL,
            realTime TEXT NOT NULL,
            type TEXT NOT NULL,
            dataJson TEXT NOT NULL
        );
        """;
}
