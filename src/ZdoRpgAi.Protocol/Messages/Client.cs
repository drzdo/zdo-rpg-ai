namespace ZdoRpgAi.Protocol.Messages;

// Client → Mod

public enum ClientToModMessageType {
    SayMp3File,
}

public record SayMp3FilePayload(string NpcId, string Mp3Name, string Text, double? DurationSec = null);

// Mod → Client

public record StartSessionAckPayload(string SessionId);

// Client → Both (Mod + Server)

public enum ClientToBothMessageType {
    PlayerStartSpeak,
    PlayerStopSpeak,
}

public record PlayerStartSpeakPayload(string PlayerId, string? TargetCharacterId, string GameTime);
public record PlayerStopSpeakPayload(string PlayerId, bool Cancel = false);

// Client → Server

public enum ClientToServerMessageType {
    PlayerSpeaksText,
    PlayerSpeaksAudio,
}

public record PlayerSpeaksTextPayload(string PlayerId, string Text, string? TargetCharacterId, string GameTime);
public record PlayerSpeaksAudioPayload(string PlayerId);
