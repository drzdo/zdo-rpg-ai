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

        CREATE TABLE story_event (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            gameTime TEXT NOT NULL,
            realTime TEXT NOT NULL,
            type TEXT NOT NULL,
            dataJson TEXT NOT NULL,
            archivedTo INTEGER
        );
        CREATE INDEX idx_story_event_archived ON story_event(archivedTo);

        CREATE TABLE story_event_observer (
            storyEventId INTEGER NOT NULL,
            characterId TEXT NOT NULL
        );
        CREATE INDEX idx_story_event_observer_character ON story_event_observer(characterId);

        CREATE TABLE story_event_summary (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            summary TEXT NOT NULL,
            realTime TEXT NOT NULL,
            archivedTo INTEGER
        );
        CREATE INDEX idx_story_event_summary_archived ON story_event_summary(archivedTo);

        CREATE TABLE story_event_summary_observer (
            summaryId INTEGER NOT NULL,
            characterId TEXT NOT NULL
        );
        CREATE INDEX idx_story_event_summary_observer_character ON story_event_summary_observer(characterId);
        """;
}
