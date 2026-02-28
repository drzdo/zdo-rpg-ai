using Microsoft.Data.Sqlite;

namespace ZdoRpgAi.Database.Migrations.Main;

public class Main001 : IMigration {
    public void Before(SqliteConnection conn) {
        conn.Execute("CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
        conn.Execute("INSERT INTO meta (key, value) VALUES ('dbtype', 'main')");
    }

    public string GetSql() => """
        CREATE TABLE topic (
            id TEXT PRIMARY KEY,
            dataJson TEXT NOT NULL,
            contentJson TEXT NOT NULL
        );

        CREATE TABLE topic_content_override (
            id TEXT PRIMARY KEY,
            topicId TEXT NOT NULL,
            sortOrder INT,
            dataJson TEXT NOT NULL,
            contentJson TEXT NOT NULL
        );

        CREATE TABLE npc (
            id TEXT PRIMARY KEY,
            dataJson TEXT NOT NULL
        );

        CREATE TABLE npc_pinned_topic (
            npcId TEXT NOT NULL,
            topicId TEXT NOT NULL,
            sortOrder INT,
            dataJson TEXT NOT NULL,
            PRIMARY KEY (npcId, topicId)
        );

        CREATE TABLE npc_action (
            id TEXT PRIMARY KEY,
            condition TEXT,
            action TEXT NOT NULL,
            title TEXT NOT NULL,
            description TEXT,
            arguments TEXT
        );

        CREATE TABLE npc_attribute (
            id TEXT PRIMARY KEY,
            dataJson TEXT NOT NULL
        );

        CREATE TABLE npc_attribute_value (
            npcId TEXT NOT NULL,
            attributeId TEXT NOT NULL,
            dataJson TEXT NOT NULL,
            PRIMARY KEY (npcId, attributeId)
        );
        """;

    public void After(SqliteConnection conn) {
    }
}
