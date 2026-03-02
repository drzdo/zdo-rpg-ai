using ZdoRpgAi.Client.App;
using ZdoRpgAi.Client.Channel;
using ZdoRpgAi.Client.Hotkey;
using ZdoRpgAi.Client.Microphone;
using ZdoRpgAi.Client.VoiceCapture;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.Client.Bootstrap;

public static class ClientBootstrap {
    public static void ResolvePaths(ClientConfig config, string configPath) {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        if (config.Mod.Provider == ModProvider.Openmw) {
            config.Mod.Openmw.DataDir = ExpandPath(config.Mod.Openmw.DataDir, baseDir);
            config.Mod.Openmw.LogFilePath = ExpandPath(config.Mod.Openmw.LogFilePath, baseDir);
        }
        if (config.Log.FilePath != null) {
            config.Log.FilePath = ExpandPath(config.Log.FilePath, baseDir);
        }
    }

    public static ClientApplication Create(ClientConfig config) {
        IChannel modChannel;
        string mp3DataDir;

        switch (config.Mod.Provider) {
            case ModProvider.Emulator:
                var emu = config.Mod.Emulator;
                modChannel = new EmulatorModChannel(emu.Host, emu.Port);
                mp3DataDir = Path.GetTempPath();
                break;
            default:
                var openmw = config.Mod.Openmw;
                modChannel = new OpenmwModChannel(openmw.DataDir, openmw.LogFilePath, openmw.PollIntervalMs);
                mp3DataDir = openmw.DataDir;
                break;
        }

        var modRpc = new RpcChannel(modChannel);
        var server = new ServerConnection(config.Server);
        var mp3MaxFiles = config.Mod.Provider == ModProvider.Openmw
            ? config.Mod.Openmw.Mp3MaxFiles
            : 10;
        var mp3 = new Mp3Manager(mp3DataDir, mp3MaxFiles);

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
        if (path.StartsWith('~')) {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
        }

        return Path.GetFullPath(path, baseDir);
    }
}
