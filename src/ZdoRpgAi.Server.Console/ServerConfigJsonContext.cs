using System.Text.Json.Serialization;
using ZdoRpgAi.Server.Bootstrap;

namespace ZdoRpgAi.Server.Console;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true
)]
[JsonSerializable(typeof(ServerConfig))]
internal partial class ServerConfigJsonContext : JsonSerializerContext;
