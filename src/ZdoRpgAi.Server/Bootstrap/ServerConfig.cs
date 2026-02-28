using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Llm.Gemini;
using ZdoRpgAi.Server.SpeechToText.Deepgram;
using ZdoRpgAi.Server.TextToSpeech.ElevenLabs;

namespace ZdoRpgAi.Server.Bootstrap;

public class ServerConfig {
    public LogConfig Log { get; set; } = new();
    public DatabaseSection Database { get; set; } = new();
    public HttpServerSection HttpServer { get; set; } = new();
    public TtsSection Tts { get; set; } = new();
    public SttSection Stt { get; set; } = new();
    public LlmSection Llm { get; set; } = new();
}

public class DatabaseSection {
    public string MainDbPath { get; set; } = "main.db";
    public string SaveGameDbPath { get; set; } = "save.db";
}

public class HttpServerSection {
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8080;
    public int MaxMessageSize { get; set; } = 10_485_760;
    public int RpcTimeoutMs { get; set; } = 5000;
    public string ClientToken { get; set; } = "";
}

public class TtsSection {
    public string Provider { get; set; } = "elevenlabs";
    public ElevenLabsConfig? ElevenLabs { get; set; }
}

public class SttSection {
    public string Provider { get; set; } = "deepgram";
    public DeepgramConfig? Deepgram { get; set; }
}

public class LlmSection {
    public string Provider { get; set; } = "gemini";
    public GeminiConfig? Gemini { get; set; }
}
