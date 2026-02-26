using System.Runtime.InteropServices;
using PortAudioSharp;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.Microphone;

public class PortAudioMicrophoneListener : IMicrophoneListener {
    private static readonly ILog Log = Logger.Get<PortAudioMicrophoneListener>();

    private readonly int _targetRate;
    private readonly int _targetFrameSamples;
    private readonly int _deviceIndex;
    private PortAudioSharp.Stream? _stream;

    private int _captureRate;
    private int _captureFrameSamples;
    private bool _needsResample;

    public event Action<ReadOnlyMemory<byte>>? FrameCaptured;

    public PortAudioMicrophoneListener(int sampleRate, int frameSizeSamples, int deviceIndex) {
        _targetRate = sampleRate;
        _targetFrameSamples = frameSizeSamples;
        _deviceIndex = deviceIndex;
    }

    public void Start() {
        PortAudio.Initialize();

        var deviceCount = PortAudio.DeviceCount;
        for (var i = 0; i < deviceCount; i++) {
            var info = PortAudio.GetDeviceInfo(i);
            Log.Info("Audio device [{Index}]: {Name}, sampleRate={Rate}Hz",
                i, info.name, info.defaultSampleRate);
        }

        var deviceIdx = _deviceIndex >= 0 ? _deviceIndex : PortAudio.DefaultInputDevice;
        if (deviceIdx < 0) {
            throw new InvalidOperationException("No audio input device found");
        }

        var deviceInfo = PortAudio.GetDeviceInfo(deviceIdx);
        Log.Info("Mic device: {Name} (index={Index}, defaultSampleRate={DefaultRate}Hz)",
            deviceInfo.name, deviceIdx, deviceInfo.defaultSampleRate);

        // Open at the device's native rate to avoid macOS CoreAudio resampling issues
        // (e.g. when another app like Morrowind has already configured the hardware rate)
        var deviceRate = (int)deviceInfo.defaultSampleRate;
        if (deviceRate != _targetRate && deviceRate > 0) {
            _captureRate = deviceRate;
            _captureFrameSamples = _targetFrameSamples * deviceRate / _targetRate;
            _needsResample = true;
            Log.Info("Will capture at {CaptureRate}Hz and resample to {TargetRate}Hz",
                _captureRate, _targetRate);
        } else {
            _captureRate = _targetRate;
            _captureFrameSamples = _targetFrameSamples;
            _needsResample = false;
        }

        var param = new StreamParameters {
            device = deviceIdx,
            channelCount = 1,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = deviceInfo.defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero
        };

        _stream = new PortAudioSharp.Stream(
            inParams: param,
            outParams: null,
            sampleRate: _captureRate,
            framesPerBuffer: (uint)_captureFrameSamples,
            streamFlags: StreamFlags.ClipOff,
            callback: AudioCallback,
            userData: IntPtr.Zero);

        Log.Info("Mic capture starting: capture={CaptureRate}Hz, target={TargetRate}Hz, {FrameSize} samples/frame",
            _captureRate, _targetRate, _captureFrameSamples);
        _stream.Start();
        Log.Info("Mic capture started");
    }

    private StreamCallbackResult AudioCallback(
        IntPtr input, IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData) {

        if (input == IntPtr.Zero || FrameCaptured == null) {
            return StreamCallbackResult.Continue;
        }

        var byteCount = (int)frameCount * 2; // 16-bit mono
        var buffer = new byte[byteCount];
        Marshal.Copy(input, buffer, 0, byteCount);

        if (_needsResample) {
            var resampled = Resample(buffer, (int)frameCount, _targetFrameSamples);
            FrameCaptured.Invoke(resampled);
        } else {
            FrameCaptured.Invoke(buffer);
        }

        return StreamCallbackResult.Continue;
    }

    /// <summary>
    /// Downsample 16-bit PCM mono using linear interpolation.
    /// </summary>
    private static byte[] Resample(byte[] source, int sourceSamples, int targetSamples) {
        var result = new byte[targetSamples * 2];
        var ratio = (double)sourceSamples / targetSamples;

        for (var i = 0; i < targetSamples; i++) {
            var srcPos = i * ratio;
            var srcIdx = (int)srcPos;
            var frac = srcPos - srcIdx;

            var s0 = (short)(source[srcIdx * 2] | (source[srcIdx * 2 + 1] << 8));
            short s1;
            if (srcIdx + 1 < sourceSamples) {
                s1 = (short)(source[(srcIdx + 1) * 2] | (source[(srcIdx + 1) * 2 + 1] << 8));
            } else {
                s1 = s0;
            }

            var interpolated = (short)(s0 + frac * (s1 - s0));
            result[i * 2] = (byte)(interpolated & 0xFF);
            result[i * 2 + 1] = (byte)((interpolated >> 8) & 0xFF);
        }

        return result;
    }

    public void CheckSampleRate() {
        var deviceIdx = _deviceIndex >= 0 ? _deviceIndex : PortAudio.DefaultInputDevice;
        if (deviceIdx < 0) return;
        var currentRate = (int)PortAudio.GetDeviceInfo(deviceIdx).defaultSampleRate;
        if (currentRate != _captureRate) {
            Log.Warn("Device sample rate changed from {CaptureRate}Hz to {CurrentRate}Hz since stream was opened. " +
                "Audio may be garbled. Restart the client to fix",
                _captureRate, currentRate);
        }
    }

    public void Stop() {
        if (_stream != null) {
            try {
                Log.Info("Mic capture stopping");
                _stream.Stop();
                Log.Info("Mic capture stopped");
            }
            catch (Exception ex) {
                Log.Warn("Error stopping mic stream: {Error}", ex.Message);
            }
        }
    }

    public void Dispose() {
        Stop();
        _stream?.Dispose();
        _stream = null;
        PortAudio.Terminate();
    }
}
