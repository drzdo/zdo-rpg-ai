using ZdoRpgAi.Core;
using ZdoRpgAi.ModEmulator.Console;
using ZdoRpgAi.Util;

var parser = new CommandLineArgsParser("Zdo RPG AI Mod Emulator", BuildInfo.Version);
parser.Add("--host", "Host to listen on", defaultValue: "localhost");
parser.Add("-p", "--port", "Port to listen on", defaultValue: "8081");

var parsed = parser.Parse(args);
var host = parsed.Get("--host")!;
var port = int.Parse(parsed.Get("--port")!);

Logger.Configure(new LogConfig { ConsoleLevel = LogLevel.Debug });
var log = Logger.Get<EmulatorServer>();
log.Info("Mod Emulator {Version}", BuildInfo.Version);

log.Info("World: Seyda Neen");
log.Info("NPCs:");
foreach (var npc in SeydaNeenWorld.Npcs) {
    log.Info("  {ObjectId}: {Name} ({Race}, {Sex})", npc.ObjectId, npc.Name, npc.Race, npc.Sex);
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

var server = new EmulatorServer(host, port);
log.Info("Listening on {Host}:{Port}", host, port);

var serverTask = server.RunAsync(cts.Token);

// Interactive command loop
var input = new ConsoleInput();
_ = Task.Run(async () => {
    await Task.Delay(500, cts.Token).ConfigureAwait(false);
    PrintHelp();

    while (!cts.Token.IsCancellationRequested) {
        var line = await Task.Run(() => input.ReadLine(), cts.Token).ConfigureAwait(false);
        if (line == null) break;

        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) continue;

        var session = server.Session;
        if (session == null && parts[0] != "help" && parts[0] != "q") {
            log.Warn("No client connected");
            continue;
        }

        switch (parts[0].ToLowerInvariant()) {
            case "target" or "t":
                if (parts.Length < 2) {
                    log.Info("Usage: target <npc_id | clear>");
                } else if (parts[1] == "clear") {
                    session!.SetTarget(null);
                } else {
                    session!.SetTarget(parts[1]);
                }
                break;
            case "say" or "s":
                if (parts.Length < 2) {
                    log.Info("Usage: say <text>");
                } else {
                    session!.SendPlayerSpeaksText(parts[1]);
                }
                break;
            case "npcs":
                foreach (var npc in SeydaNeenWorld.Npcs) {
                    log.Info("  {ObjectId}: {Name} ({Race}, {Sex})", npc.ObjectId, npc.Name, npc.Race, npc.Sex);
                }
                break;
            case "help":
                PrintHelp();
                break;
            case "q":
                cts.Cancel();
                break;
            default:
                log.Warn("Unknown command: {Cmd}. Type 'help' for commands.", parts[0]);
                break;
        }
    }
}, cts.Token);

try {
    await serverTask;
}
catch (OperationCanceledException) {
    // Normal shutdown
}

void PrintHelp() {
    log.Info("Commands:");
    log.Info("  say <text>       - Send player speech text to server (alias: s)");
    log.Info("  target <npc_id>  - Set player target NPC (alias: t)");
    log.Info("  target clear     - Clear player target");
    log.Info("  npcs             - List available NPCs");
    log.Info("  help             - Show this help");
    log.Info("  q                - Quit");
}
