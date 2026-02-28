using Microsoft.Data.Sqlite;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Database;

public class MigrationRunner {
    static readonly ILog Log = Logger.Get<MigrationRunner>();

    private readonly IMigration[] _migrations;
    private readonly string _dbType;

    public MigrationRunner(IMigration[] migrations, string dbType) {
        _migrations = migrations;
        _dbType = dbType;
    }

    public void Run(SqliteConnection conn) {
        var applied = GetAppliedMigrations(conn);
        var builtIn = _migrations.Select(MigrationId).ToList();

        // Applied must be a prefix of builtIn
        for (var i = 0; i < applied.Count; i++) {
            if (i >= builtIn.Count) {
                var unknown = applied.Skip(i).ToList();
                Log.Error("Database contains unknown migrations: {Migrations}. App may be outdated",
                    string.Join(", ", unknown));
                throw new InvalidOperationException(
                    $"Database contains unknown migrations: {string.Join(", ", unknown)}");
            }

            if (applied[i] != builtIn[i]) {
                Log.Error("Migration order mismatch at position {Position}: expected '{Expected}', got '{Actual}'",
                    i, builtIn[i], applied[i]);
                throw new InvalidOperationException(
                    $"Migration order mismatch at position {i}: expected '{builtIn[i]}', got '{applied[i]}'");
            }
        }

        var pending = _migrations.Skip(applied.Count).ToList();

        if (pending.Count == 0) {
            Log.Debug("{DbType} database is up to date ({Count} migrations applied)", _dbType, applied.Count);
            return;
        }

        foreach (var migration in pending) {
            Log.Info("Applying migration {Id} to {DbType} database", MigrationId(migration), _dbType);

            using var tx = conn.BeginTransaction();
            migration.Before(conn);
            conn.Execute(migration.GetSql());
            migration.After(conn);
            tx.Commit();
        }

        SaveAppliedMigrations(conn, builtIn);
        Log.Info("{DbType} database migrated: {Count} migration(s) applied", _dbType, pending.Count);
    }

    private static List<string> GetAppliedMigrations(SqliteConnection conn) {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='meta'";
        if ((long)check.ExecuteScalar()! == 0)
            return [];

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = 'applied_migrations'";
        var result = cmd.ExecuteScalar();

        if (result is not string s || string.IsNullOrEmpty(s))
            return [];

        return s.Split(',').ToList();
    }

    private static string MigrationId(IMigration m) => m.GetType().Name;

    private static void SaveAppliedMigrations(SqliteConnection conn, List<string> migrations) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO meta (key, value) VALUES ('applied_migrations', @v)
            ON CONFLICT(key) DO UPDATE SET value = @v
            """;
        cmd.Parameters.AddWithValue("@v", string.Join(",", migrations));
        cmd.ExecuteNonQuery();
    }
}
