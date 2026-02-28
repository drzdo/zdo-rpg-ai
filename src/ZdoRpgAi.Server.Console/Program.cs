using ZdoRpgAi.Core;
using ZdoRpgAi.Server.App;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Console;
using ZdoRpgAi.Util;

var parser = new CommandLineArgsParser("Zdo RPG AI Server", BuildInfo.Version);
parser.Add("-c", "--config", "Path to YAML config file", defaultValue: "server-config.yaml");

var parsed = parser.Parse(args);
var configPath = parsed.Get("--config")!;
var config = ConfigParser.ParseYamlFile(configPath, ServerConfigJsonContext.Default.ServerConfig);

ServerBootstrap.ResolvePaths(config, configPath);
Logger.Configure(config.Log);
Logger.Get<ServerApplication>().Info("Server {Version}", BuildInfo.Version);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

using var app = ServerBootstrap.Create(config);
try {
    await app.RunAsync(cts.Token);
}
catch (OperationCanceledException) {
    // Normal shutdown
}
catch (Exception ex) {
    Logger.Get<ServerApplication>().Error(ex, "Fatal error");
    Logger.Flush();
    throw;
}
