namespace ZdoRpgAi.Protocol.Messages;

public enum ClientToModMessageType {
    Hello,
    SayMp3File,
    PlayerSpeechRecognitionInProgress,
    PlayerSpeechRecognized,
    PlayerStartSpeak,
    PlayerStopSpeak,
}

// Payloads
public record HelloPayload();
public record SayMp3FilePayload(string NpcId, string Mp3Name, string Text, double? DurationSec = null);
