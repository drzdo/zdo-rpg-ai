using ZdoRpgAi.Database;

namespace ZdoRpgAi.Repository;

public class LocalDatabaseMainRepository : IMainRepository, IDisposable {
    private readonly MainDatabase _db;

    public LocalDatabaseMainRepository(string path) {
        _db = new MainDatabase(path);
        _db.Open();
    }

    public void Dispose() {
        _db.Dispose();
    }
}
