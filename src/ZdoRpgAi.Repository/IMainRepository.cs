namespace ZdoRpgAi.Repository;

public interface IMainRepository : IDisposable {
    RawNpcInfo? GetNpcInfo(string npcId);
}
