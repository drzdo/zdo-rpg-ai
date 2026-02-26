using ZdoRpgAi.Client.App;
using ZdoRpgAi.Client.Bootstrap;
using ZdoRpgAi.Core;
using ZdoRpgAi.Util;

var parser = new CommandLineArgsParser("Zdo RPG AI Client", BuildInfo.Version);
parser.Add("-c", "--config", "Path to JSON config file", defaultValue: "config.json");

var parsed = parser.Parse(args);
var configPath = parsed.Get("--config")!;
var config = ConfigParser.FromFile(configPath, ClientConfigJsonContext.Default.ClientConfig);

ClientBootstrap.ResolvePaths(config, configPath);
Logger.Configure(config.Log);
Logger.Get<ClientApplication>().Info("Client {Version}", BuildInfo.Version);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

using var app = ClientBootstrap.Create(config);
try {
    await app.RunAsync(cts.Token);
}
catch (OperationCanceledException) {
    // Normal shutdown
}
catch (Exception ex) {
    Logger.Get<ClientApplication>().Error(ex, "Fatal error");
    Logger.Flush();
    throw;
}
