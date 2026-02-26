namespace ZdoRpgAi.Protocol.Messages;

public enum ClientToServerMessageType {
    PlayerSpeaks,
    PlayerSpeaksAudio,
    PlayerStartSpeak,
    PlayerStopSpeak,
}

// Payloads
public record PlayerSpeaksPayload(string PlayerId, string Text, string? TargetCharacterId, string GameTime);
public record PlayerSpeaksAudioPayload(string PlayerId);
public record PlayerStartSpeakPayload(string PlayerId, string? TargetCharacterId, string GameTime);
public record PlayerStopSpeakPayload(string PlayerId);
