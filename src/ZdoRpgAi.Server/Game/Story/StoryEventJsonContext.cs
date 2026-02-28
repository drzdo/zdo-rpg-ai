using System.Text.Json.Serialization;

namespace ZdoRpgAi.Server.Game.Story;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StoryEvent))]
internal partial class StoryEventJsonContext : JsonSerializerContext;
