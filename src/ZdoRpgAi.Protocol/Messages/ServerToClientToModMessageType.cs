namespace ZdoRpgAi.Protocol.Messages;

public enum ServerToClientToModMessageType {
    NpcSpeaks,
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

// RPC payloads
public record GetCharactersWhoHearRequestPayload(string CharacterId);
public record GetNpcInfoRequestPayload(string NpcId);
public record GetNpcInfoResponsePayload(string ObjectId, string Name, string Race, string Sex);
public record GetPlayerInfoRequestPayload(string PlayerId);
public record GetPlayerInfoResponsePayload(string ObjectId, string Name, string Race, string Sex);

// Fire-and-forget payloads
public record SpawnOnGroundInFrontOfCharacterPayload(string NpcId, string ItemId, int Count = 1);
public record PlaySound3dOnCharacterPayload(string NpcId, string Sound);
public record NpcStartFollowCharacterPayload(string NpcId, string TargetCharacterId);
public record NpcStopFollowCharacterPayload(string NpcId);
public record NpcAttackPayload(string NpcId, string TargetCharacterId);
public record NpcStopAttackPayload(string NpcId);
public record ShowMessageBoxPayload(string Message);
