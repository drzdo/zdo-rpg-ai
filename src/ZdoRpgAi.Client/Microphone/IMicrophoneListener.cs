namespace ZdoRpgAi.Client.Microphone;

public interface IMicrophoneListener : IDisposable {
    event Action<ReadOnlyMemory<byte>>? FrameCaptured;

    void Start();
    void Stop();
    void CheckSampleRate();
}
