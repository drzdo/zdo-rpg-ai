using System.Text.Json.Serialization;

namespace ZdoRpgAi.Server.Game.Story;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PlayerSpeak), "PlayerSpeak")]
[JsonDerivedType(typeof(NpcSpeak), "NpcSpeak")]
public abstract record StoryEvent {
    public long Id { get; init; } = -1;
    public required string GameTime { get; init; }
    public string RealTime { get; init; } = "";

    public static T Create<T>(T evt) where T : StoryEvent =>
        evt with { RealTime = GetRealTime() };

    private static string GetRealTime() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    public sealed record PlayerSpeak : StoryEvent {
        public required string PlayerCharacterId { get; init; }
        public string? TargetCharacterId { get; init; }
        public required string Text { get; init; }
    }

    public sealed record NpcSpeak : StoryEvent {
        public required string NpcCharacterId { get; init; }
        public string? TargetCharacterId { get; init; }
        public required string Text { get; init; }
    }
}
