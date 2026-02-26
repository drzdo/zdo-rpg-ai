using System.Threading.Channels;
using ZdoRpgAi.Client.Hotkey;
using ZdoRpgAi.Client.Microphone;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.VoiceCapture;

public enum PttSubMode {
    Hold,
    Toggle
}

public class PushToTalkConfig {
    public string Key { get; set; } = "Y";
    public PttSubMode SubMode { get; set; } = PttSubMode.Hold;
    public int ReleaseDelayMs { get; set; } = 400;
}

public class VoiceCaptureServiceConfig {
    public bool Enabled { get; set; } = false;
    public int SampleRate { get; set; } = 16000;
    public int FrameSizeMs { get; set; } = 20;
    public int DeviceIndex { get; set; } = -1;
    public PushToTalkConfig PushToTalk { get; set; } = new();

    public string PttKey => PushToTalk.Key;
    public PttSubMode PttSubMode => PushToTalk.SubMode;
    public int PttReleaseDelayMs => PushToTalk.ReleaseDelayMs;
    public int FrameSizeSamples => SampleRate * FrameSizeMs / 1000;
    public int FrameSizeBytes => FrameSizeSamples * 2; // 16-bit mono
}

public class VoiceCaptureService : IDisposable {
    private static readonly ILog Log = Logger.Get<VoiceCaptureService>();

    private readonly VoiceCaptureServiceConfig _config;
    private readonly IMicrophoneListener _capture;
    private readonly IHotkeyListener? _hotkey;

    private readonly System.Threading.Channels.Channel<byte[]> _audioChannel;
    private volatile bool _isActive;
    private int _activeFrameCount;
    private int _activeZeroFrameCount;
    private CancellationTokenSource? _deactivateDelayCts;

    public event Action? Activated;
    public event Action? Deactivated;
    public Func<byte[], Task>? OnAudioBufferAsync { get; set; }

    public VoiceCaptureService(VoiceCaptureServiceConfig config, IMicrophoneListener capture, IHotkeyListener? hotkey) {
        _config = config;
        _capture = capture;
        _capture.FrameCaptured += HandleFrameCaptured;
        _hotkey = hotkey;

        _audioChannel = System.Threading.Channels.Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        });

        if (_hotkey != null) {
            _hotkey.KeyPressed += HandleKeyPressed;
            _hotkey.KeyReleased += HandleKeyReleased;
        }
    }

    public async Task RunAsync(CancellationToken ct) {
        Log.Info("Mic system starting: rate={Rate}Hz, frame={FrameMs}ms",
            _config.SampleRate, _config.FrameSizeMs);

        _capture.Start();

        var tasks = new List<Task>();

        tasks.Add(DrainAudioChannelAsync(ct));

        if (_hotkey != null) {
            tasks.Add(_hotkey.RunAsync(ct));
        }

        try {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) {
            // Normal shutdown
        }
        finally {
            _capture.Stop();
        }
    }

    private void HandleFrameCaptured(ReadOnlyMemory<byte> frame) {
        // Only enqueue audio when active
        if (!_isActive) return;

        _activeFrameCount++;
        if (frame.Span.IndexOfAnyExcept((byte)0) < 0) {
            _activeZeroFrameCount++;
        }

        var copy = frame.ToArray();
        _audioChannel.Writer.TryWrite(copy);
    }

    private async Task DrainAudioChannelAsync(CancellationToken ct) {
        try {
            await foreach (var buffer in _audioChannel.Reader.ReadAllAsync(ct)) {
                if (OnAudioBufferAsync != null) {
                    await OnAudioBufferAsync(buffer);
                }
            }
        }
        catch (OperationCanceledException) {
            // Normal shutdown
        }
    }

    // --- Push-to-talk handlers ---

    private void HandleKeyPressed() {
        if (_config.PttSubMode == PttSubMode.Hold) {
            CancelPendingDeactivation();
            Activate();
        }
        else {
            // Toggle mode
            if (_isActive)
                Deactivate();
            else
                Activate();
        }
    }

    private void HandleKeyReleased() {
        if (_config.PttSubMode == PttSubMode.Hold && _isActive) {
            if (_config.PttReleaseDelayMs > 0) {
                CancelPendingDeactivation();
                _deactivateDelayCts = new CancellationTokenSource();
                var ct = _deactivateDelayCts.Token;
                _ = DeactivateAfterDelayAsync(ct);
            }
            else {
                Deactivate();
            }
        }
    }

    private void CancelPendingDeactivation() {
        _deactivateDelayCts?.Cancel();
        _deactivateDelayCts?.Dispose();
        _deactivateDelayCts = null;
    }

    private async Task DeactivateAfterDelayAsync(CancellationToken ct) {
        try {
            await Task.Delay(_config.PttReleaseDelayMs, ct);
            Deactivate();
        }
        catch (OperationCanceledException) {
            // Key pressed again during delay — deactivation cancelled
        }
    }

    // --- Activation ---

    private void Activate() {
        if (_isActive) return;
        _capture.CheckSampleRate();
        _activeFrameCount = 0;
        _activeZeroFrameCount = 0;
        _isActive = true;
        Log.Info("Mic activated");
        ThreadPool.QueueUserWorkItem(_ => Activated?.Invoke());
    }

    private void Deactivate() {
        if (!_isActive) return;
        _isActive = false;
        if (_activeFrameCount > 0 && _activeFrameCount == _activeZeroFrameCount) {
            Log.Warn("All {Count} mic frames were silent (all zeroes). Check macOS microphone permissions for your terminal app (System Settings > Privacy & Security > Microphone)", _activeFrameCount);
        }
        Log.Info("Mic deactivated");
        ThreadPool.QueueUserWorkItem(_ => Deactivated?.Invoke());
    }

    public void Dispose() {
        CancelPendingDeactivation();
        _hotkey?.Dispose();
        _capture.Dispose();
        _audioChannel.Writer.Complete();
    }
}
