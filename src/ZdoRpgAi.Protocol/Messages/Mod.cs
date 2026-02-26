namespace ZdoRpgAi.Protocol.Messages;

// Mod → Server

public enum ModToServerMessageType {
    PlayerAdded,
    TargetChanged,
    CellChange,
    GameSaveLoad,
    GetCharactersWhoHearResponse,
}

public record PlayerAddedPayload(string PlayerId);
public record TargetChangedPayload(string PlayerId, string? NpcId);
public record CellChangePayload(string PlayerId, string CellName);
public record GameSaveLoadPayload();
public record NearbyCharacterInfo(string CharacterId, float Distance);
public record GetCharactersWhoHearResponsePayload(NearbyCharacterInfo[] Characters);
