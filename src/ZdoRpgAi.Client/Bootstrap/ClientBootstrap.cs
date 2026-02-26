using ZdoRpgAi.Client.App;
using ZdoRpgAi.Client.Channel;
using ZdoRpgAi.Client.Hotkey;
using ZdoRpgAi.Client.Microphone;
using ZdoRpgAi.Client.VoiceCapture;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.Client.Bootstrap;

public static class ClientBootstrap {
    public static void ResolvePaths(ClientConfig config, string configPath) {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        config.Mod.DataDir = ExpandPath(config.Mod.DataDir, baseDir);
        config.Mod.LogFilePath = ExpandPath(config.Mod.LogFilePath, baseDir);
        if (config.Log.FilePath != null)
            config.Log.FilePath = ExpandPath(config.Log.FilePath, baseDir);
    }

    public static ClientApplication Create(ClientConfig config) {
        var modChannel = new OpenmwModChannel(config.Mod.DataDir, config.Mod.LogFilePath, config.Mod.PollIntervalMs);
        var modRpc = new RpcChannel(modChannel);
        var server = new ServerConnection(config.Server);
        var mp3 = new Mp3Manager(config.Mod.DataDir, config.Mod.Mp3MaxFiles);

        VoiceCaptureService? voiceCapture = null;
        if (config.VoiceCapture is { Enabled: true } vc) {
            var mic = new PortAudioMicrophoneListener(vc.SampleRate, vc.FrameSizeSamples, vc.DeviceIndex);
            var hotkey = new MacosHotkeyListener(vc.PttKey);
            voiceCapture = new VoiceCaptureService(vc, mic, hotkey);
        }

        var bridge = new ClientChannelBridge(server, modRpc);
        return new ClientApplication(bridge, mp3, voiceCapture, config.StripDiacritics);
    }

    private static string ExpandPath(string path, string baseDir) {
        if (path.StartsWith('~'))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
        return Path.GetFullPath(path, baseDir);
    }
}
