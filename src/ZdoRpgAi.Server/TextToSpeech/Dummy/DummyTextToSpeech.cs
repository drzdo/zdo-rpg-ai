namespace ZdoRpgAi.Server.TextToSpeech.Dummy;

public class DummyTextToSpeech : ITextToSpeech {
    public Task<Mp3Data> GenerateAsync(string text, IVoiceInfo voiceInfo) =>
        Task.FromResult(new Mp3Data(new byte[128]));
}
