using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.App;
using ZdoRpgAi.Server.Http;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Llm.Dummy;
using ZdoRpgAi.Server.Llm.Gemini;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.SpeechToText.Deepgram;
using ZdoRpgAi.Server.SpeechToText.Dummy;
using ZdoRpgAi.Server.TextToSpeech;
using ZdoRpgAi.Server.TextToSpeech.Dummy;
using ZdoRpgAi.Server.TextToSpeech.ElevenLabs;

namespace ZdoRpgAi.Server.Bootstrap;

public static class ServerBootstrap {
    private static readonly ILog Log = Logger.Get<ServerApplication>();

    public static void ResolvePaths(ServerConfig config, string configPath) {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        config.Database.MainDbPath = ExpandPath(config.Database.MainDbPath, baseDir);
        config.Database.SaveGameDbPath = ExpandPath(config.Database.SaveGameDbPath, baseDir);
        if (config.Log.FilePath != null)
            config.Log.FilePath = ExpandPath(config.Log.FilePath, baseDir);
    }

    public static ServerApplication Create(ServerConfig config) {
        var mainRepo = new LocalDatabaseMainRepository(config.Database.MainDbPath);
        var saveGameRepo = new LocalDatabaseSaveGameRepository(config.Database.SaveGameDbPath);
        var tts = CreateTts(config.Tts);
        var stt = CreateStt(config.Stt);
        var llm = CreateLlm(config.Llm);
        var lua = new LuaSandbox();
        var httpServer = new HttpServer(config.HttpServer);

        return new ServerApplication(mainRepo, saveGameRepo, tts, stt, llm, lua, httpServer);
    }

    private static ITextToSpeech CreateTts(TtsSection config) {
        Log.Info("Creating text-to-speech: {Provider}", config.Provider);
        return config.Provider switch {
        "dummy" => new DummyTextToSpeech(),
        "elevenlabs" => new ElevenLabsTextToSpeech(config.ElevenLabs
            ?? throw new InvalidOperationException("tts.elevenLabs config is required when provider is 'elevenlabs'")),
        _ => throw new InvalidOperationException($"Unknown TTS provider: {config.Provider}"),
    };
    }

    private static ISpeechToText CreateStt(SttSection config) {
        Log.Info("Creating speech-to-text: {Provider}", config.Provider);
        return config.Provider switch {
        "dummy" => new DummySpeechToText(),
        "deepgram" => new DeepgramSpeechToText(config.Deepgram
            ?? throw new InvalidOperationException("stt.deepgram config is required when provider is 'deepgram'")),
        _ => throw new InvalidOperationException($"Unknown STT provider: {config.Provider}"),
    };
    }

    private static ILlm CreateLlm(LlmSection config) {
        Log.Info("Creating LLM: {Provider}", config.Provider);
        return config.Provider switch {
        "dummy" => new DummyLlm(),
        "gemini" => new GeminiLlm(config.Gemini
            ?? throw new InvalidOperationException("llm.gemini config is required when provider is 'gemini'")),
        _ => throw new InvalidOperationException($"Unknown LLM provider: {config.Provider}"),
    };
    }

    private static string ExpandPath(string path, string baseDir) {
        if (path.StartsWith('~'))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
        return Path.GetFullPath(path, baseDir);
    }
}
