namespace ZdoRpgAi.Protocol.Messages;

public enum ServerToClientMessageType {
    NpcSpeaksMp3,
    SpeechRecognitionInProgress,
    SpeechRecognitionComplete,
}

// Payloads
public record NpcSpeaksPayload(string NpcId, string Text, double? DurationSec = null);
public record SpeechRecognitionInProgressPayload(string PlayerId, string Text);
public record SpeechRecognitionCompletePayload(string PlayerId, string Text);
