namespace ZdoRpgAi.Client.Hotkey;

public interface IHotkeyListener : IDisposable {
    event Action? KeyPressed;
    event Action? KeyReleased;

    Task RunAsync(CancellationToken ct);
}
