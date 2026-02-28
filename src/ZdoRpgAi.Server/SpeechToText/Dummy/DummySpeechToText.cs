namespace ZdoRpgAi.Server.SpeechToText.Dummy;

public class DummySpeechToText : ISpeechToText {
    public event Action<string>? InterimResultReceived { add { } remove { } }
    public event Action<string>? FinalResultReceived;

    public void Start() { }

    public void FeedAudio(ReadOnlyMemory<byte> buffer) { }

    public void Finish() {
        FinalResultReceived?.Invoke("This is dummy speech recognition");
    }

    public void Cancel() { }

    public void Dispose() { }
}
