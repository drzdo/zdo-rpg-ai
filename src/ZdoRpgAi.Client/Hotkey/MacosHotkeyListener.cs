using System.Runtime.InteropServices;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.Hotkey;

public class MacosHotkeyListener : IHotkeyListener {
    private static readonly ILog Log = Logger.Get<MacosHotkeyListener>();

    private readonly ushort _macKeyCode;
    private readonly string _keyName;
    private readonly int _pollIntervalMs;
    private volatile bool _wasPressed;
    private CancellationTokenSource? _cts;

    public event Action? KeyPressed;
    public event Action? KeyReleased;

    public MacosHotkeyListener(string keyName, int pollIntervalMs = 30) {
        _keyName = keyName;
        _macKeyCode = MapMacKeyCode(keyName);
        _pollIntervalMs = pollIntervalMs;
    }

    public async Task RunAsync(CancellationToken ct) {
        Log.Info("Global hotkey: {Key} (macOS keyCode=0x{Code:X2}, polling every {Ms}ms)",
            _keyName, _macKeyCode, _pollIntervalMs);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cts = linked;

        try {
            while (!linked.Token.IsCancellationRequested) {
                var isPressed = CGEventSourceKeyState(
                    0, // kCGEventSourceStateCombinedSessionState
                    _macKeyCode);

                if (isPressed && !_wasPressed) {
                    _wasPressed = true;
                    Log.Debug("Hotkey pressed: {Key}", _keyName);
                    KeyPressed?.Invoke();
                } else if (!isPressed && _wasPressed) {
                    _wasPressed = false;
                    Log.Debug("Hotkey released: {Key}", _keyName);
                    KeyReleased?.Invoke();
                }

                await Task.Delay(_pollIntervalMs, linked.Token);
            }
        } catch (OperationCanceledException) {
            // Normal shutdown
        }

        Log.Debug("Hotkey polling stopped");
    }

    // macOS CoreGraphics — checks if a key is currently held down.
    // Works globally (any app focused), no accessibility permission required.
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern bool CGEventSourceKeyState(int stateID, ushort keycode);

    // macOS virtual key codes (kVK_* from Carbon/Events.h)
    private static ushort MapMacKeyCode(string keyName) {
        return keyName.ToUpperInvariant() switch {
            "A" => 0x00, "S" => 0x01, "D" => 0x02, "F" => 0x03,
            "H" => 0x04, "G" => 0x05, "Z" => 0x06, "X" => 0x07,
            "C" => 0x08, "V" => 0x09, "B" => 0x0B, "Q" => 0x0C,
            "W" => 0x0D, "E" => 0x0E, "R" => 0x0F, "Y" => 0x10,
            "T" => 0x11, "1" => 0x12, "2" => 0x13, "3" => 0x14,
            "4" => 0x15, "6" => 0x16, "5" => 0x17, "9" => 0x19,
            "7" => 0x1A, "8" => 0x1C, "0" => 0x1D,
            "O" => 0x1F, "U" => 0x20, "I" => 0x22, "P" => 0x23,
            "L" => 0x25, "J" => 0x26, "K" => 0x28, "N" => 0x2D,
            "M" => 0x2E,
            "SPACE" => 0x31, "TAB" => 0x30,
            "ESCAPE" or "ESC" => 0x35, "CAPSLOCK" => 0x39,
            "F1" => 0x7A, "F2" => 0x78, "F3" => 0x63, "F4" => 0x76,
            "F5" => 0x60, "F6" => 0x61, "F7" => 0x62, "F8" => 0x64,
            "F9" => 0x65, "F10" => 0x6D, "F11" => 0x67, "F12" => 0x6F,
            "LEFTSHIFT" or "LSHIFT" => 0x38,
            "RIGHTSHIFT" or "RSHIFT" => 0x3C,
            "LEFTCONTROL" or "LCONTROL" or "LEFTCTRL" or "LCTRL" => 0x3B,
            "RIGHTCONTROL" or "RCONTROL" or "RIGHTCTRL" or "RCTRL" => 0x3E,
            "LEFTALT" or "LALT" or "LEFTOPTION" or "LOPTION" => 0x3A,
            "RIGHTALT" or "RALT" or "RIGHTOPTION" or "ROPTION" => 0x3D,
            "LEFTCOMMAND" or "LCOMMAND" or "LEFTMETA" or "LMETA" => 0x37,
            "RIGHTCOMMAND" or "RCOMMAND" or "RIGHTMETA" or "RMETA" => 0x36,
            _ => throw new ArgumentException(
                $"Unknown key '{keyName}'. Supported: A-Z, 0-9, F1-F12, Space, Tab, Escape, modifier keys")
        };
    }

    public void Dispose() {
        _cts?.Cancel();
    }
}
