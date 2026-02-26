namespace ZdoRpgAi.Protocol.Messages;

// Server → Client

public enum ServerToClientMessageType {
    NpcSpeaksMp3,
}

public record NpcSpeaksMp3Payload(string NpcId, string Text, double DurationSec);

// Server → Mod

public enum ServerToModMessageType {
    NpcSpeaks,
    SpeechRecognitionInProgress,
    SpeechRecognitionComplete,
    GetCharactersWhoHear,
    GetNpcInfo,
    GetPlayerInfo,
    SpawnOnGroundInFrontOfCharacter,
    PlaySound3dOnCharacter,
    NpcStartFollowCharacter,
    NpcStopFollowCharacter,
    NpcAttack,
    NpcStopAttack,
    ShowMessageBox,
}

public record NpcSpeaksPayload();
public record SpeechRecognitionInProgressPayload(string PlayerId, string Text);
public record SpeechRecognitionCompletePayload(string PlayerId, string Text);
public record GetCharactersWhoHearRequestPayload(string CharacterId);
public record GetNpcInfoRequestPayload(string NpcId);
public record GetNpcInfoResponsePayload(string ObjectId, string Name, string Race, string Sex);
public record GetPlayerInfoRequestPayload(string PlayerId);
public record GetPlayerInfoResponsePayload(string ObjectId, string Name, string Race, string Sex);
public record SpawnOnGroundInFrontOfCharacterPayload(string NpcId, string ItemId, int Count = 1);
public record PlaySound3dOnCharacterPayload(string NpcId, string Sound);
public record NpcStartFollowCharacterPayload(string NpcId, string TargetCharacterId);
public record NpcStopFollowCharacterPayload(string NpcId);
public record NpcAttackPayload(string NpcId, string TargetCharacterId);
public record NpcStopAttackPayload(string NpcId);
public record ShowMessageBoxPayload(string Message);
