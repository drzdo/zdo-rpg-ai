using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Director;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.TextToSpeech;

namespace ZdoRpgAi.Server.Game;

public class GameRunner {
    private static readonly ILog Log = Logger.Get<GameRunner>();

    private readonly IMainRepository _mainRepo;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly ITextToSpeech _tts;
    private readonly ISpeechToText _stt;
    private readonly LuaSandbox _lua;
    private readonly PlayerMessageHandler _playerHandler;
    private readonly StoryComposer _storyComposer;
    private readonly NpcRepository _npcRepo;
    private readonly Director.Director _director;

    public GameRunner(
        IMainRepository mainRepo, ISaveGameRepository saveGameRepo,
        ITextToSpeech tts, ISpeechToText stt, ILlm mainLlm, ILlm simpleLlm, LuaSandbox lua,
        DirectorSection directorConfig) {
        _mainRepo = mainRepo;
        _saveGameRepo = saveGameRepo;
        _tts = tts;
        _stt = stt;
        _lua = lua;

        var summaryBuilder = new StorySummaryBuilder(simpleLlm);
        var story = new Story.Story(saveGameRepo, summaryBuilder, directorConfig);
        _playerHandler = new PlayerMessageHandler(stt);
        _storyComposer = new StoryComposer(story);
        _npcRepo = new NpcRepository(mainRepo, saveGameRepo);
        _director = new Director.Director(story, _storyComposer, mainLlm, simpleLlm, _npcRepo);

        _playerHandler.PlayerSpoke += _storyComposer.OnPlayerSpeak;
    }

    public void SetActiveClient(IRpcChannel? rpc) {
        if (rpc != null) {
            _playerHandler.OnClientConnected(rpc);
            _storyComposer.OnClientConnected(rpc);
            _npcRepo.SetClient(rpc);
            _director.SetClient(rpc);
        }
        else {
            _playerHandler.OnClientDisconnected();
            _storyComposer.OnClientDisconnected();
            _npcRepo.SetClient(null);
            _director.SetClient(null);
        }
    }
}
