using System.Text.Json.Serialization;

namespace ZdoRpgAi.Client.Bootstrap;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true
)]
[JsonSerializable(typeof(ClientConfig))]
public partial class ClientConfigJsonContext : JsonSerializerContext;
