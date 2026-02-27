using ZdoRpgAi.Server.App;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Llm.Gemini;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.SpeechToText.Deepgram;
using ZdoRpgAi.Server.TextToSpeech;
using ZdoRpgAi.Server.TextToSpeech.ElevenLabs;

namespace ZdoRpgAi.Server.Bootstrap;

public static class ServerBootstrap {
    public static ServerApplication Create(ServerConfig config) {
        var tts = CreateTts(config.Tts);
        var stt = CreateStt(config.Stt);
        var llm = CreateLlm(config.Llm);
        var lua = new LuaSandbox();

        return new ServerApplication(tts, stt, llm, lua);
    }

    private static ITextToSpeech CreateTts(TtsSection config) => config.Provider switch {
        "elevenlabs" => new ElevenLabsTextToSpeech(config.ElevenLabs
            ?? throw new InvalidOperationException("Tts.ElevenLabs config is required when provider is 'elevenlabs'")),
        _ => throw new InvalidOperationException($"Unknown TTS provider: {config.Provider}"),
    };

    private static ISpeechToText CreateStt(SttSection config) => config.Provider switch {
        "deepgram" => new DeepgramSpeechToText(config.Deepgram
            ?? throw new InvalidOperationException("Stt.Deepgram config is required when provider is 'deepgram'")),
        _ => throw new InvalidOperationException($"Unknown STT provider: {config.Provider}"),
    };

    private static ILlm CreateLlm(LlmSection config) => config.Provider switch {
        "gemini" => new GeminiLlm(config.Gemini
            ?? throw new InvalidOperationException("Llm.Gemini config is required when provider is 'gemini'")),
        _ => throw new InvalidOperationException($"Unknown LLM provider: {config.Provider}"),
    };
}
