using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Http;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.TextToSpeech;

namespace ZdoRpgAi.Server.App;

public class ServerApplication : IDisposable {
    private static readonly ILog Log = Logger.Get<ServerApplication>();

    private readonly IMainRepository _mainRepo;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly ITextToSpeech _tts;
    private readonly ISpeechToText _stt;
    private readonly ILlm _llm;
    private readonly LuaSandbox _lua;
    private readonly HttpServer _httpServer;

    public ServerApplication(
        IMainRepository mainRepo, ISaveGameRepository saveGameRepo,
        ITextToSpeech tts, ISpeechToText stt, ILlm llm, LuaSandbox lua,
        HttpServer httpServer) {
        _mainRepo = mainRepo;
        _saveGameRepo = saveGameRepo;
        _tts = tts;
        _stt = stt;
        _llm = llm;
        _lua = lua;
        _httpServer = httpServer;
    }

    public async Task RunAsync(CancellationToken ct) {
        Log.Info("Server started");
        await _httpServer.StartAsync(ct);
        Log.Info("Server stopped");
    }

    public void Dispose() {
        (_stt as IDisposable)?.Dispose();
        _saveGameRepo.Dispose();
        _mainRepo.Dispose();
    }
}
