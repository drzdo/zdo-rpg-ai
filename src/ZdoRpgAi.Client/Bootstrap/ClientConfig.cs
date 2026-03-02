using ZdoRpgAi.Client.App;
using ZdoRpgAi.Client.VoiceCapture;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.Bootstrap;

public class ClientConfig {
    public LogConfig Log { get; set; } = new();
    public ServerConnectionConfig Server { get; set; } = new();
    public ModConnectionConfig Mod { get; set; } = new();
    public VoiceCaptureServiceConfig? VoiceCapture { get; set; }
    public bool StripDiacritics { get; set; }
}

public enum ModProvider {
    Openmw,
    Emulator,
}

public class ModConnectionConfig {
    public ModProvider Provider { get; set; } = ModProvider.Openmw;
    public EmulatorModConnectionConfig Emulator { get; set; } = new();
    public OpenmwModConnectionConfig Openmw { get; set; } = new();
}

public class OpenmwModConnectionConfig {
    public string DataDir { get; set; } = "";
    public string LogFilePath { get; set; } = "";
    public int Mp3MaxFiles { get; set; } = 10;
    public int PollIntervalMs { get; set; } = 50;
}

public class EmulatorModConnectionConfig {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8081;
}
