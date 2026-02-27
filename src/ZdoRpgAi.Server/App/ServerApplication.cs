using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.TextToSpeech;

namespace ZdoRpgAi.Server.App;

public class ServerApplication : IDisposable {
    private static readonly ILog Log = Logger.Get<ServerApplication>();

    private readonly ITextToSpeech _tts;
    private readonly ISpeechToText _stt;
    private readonly ILlm _llm;
    private readonly LuaSandbox _lua;

    public ServerApplication(ITextToSpeech tts, ISpeechToText stt, ILlm llm, LuaSandbox lua) {
        _tts = tts;
        _stt = stt;
        _llm = llm;
        _lua = lua;
    }

    public async Task RunAsync(CancellationToken ct) {
        Log.Info("Server started");

        try {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) {
            // Expected on shutdown
        }

        Log.Info("Server stopped");
    }

    public void Dispose() {
        (_stt as IDisposable)?.Dispose();
    }
}
