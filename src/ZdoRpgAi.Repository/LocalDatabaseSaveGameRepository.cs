using ZdoRpgAi.Database;

namespace ZdoRpgAi.Repository;

public class LocalDatabaseSaveGameRepository : ISaveGameRepository, IDisposable {
    private readonly SaveGameDatabase _db;

    public LocalDatabaseSaveGameRepository(string path) {
        _db = new SaveGameDatabase(path);
        _db.Open();
    }

    public void Dispose() {
        _db.Dispose();
    }
}
