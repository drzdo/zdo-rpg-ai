namespace ZdoRpgAi.Protocol.Messages;

public enum ModToClientToServerMessageType {
    PlayerAdded,
    TargetChanged,
    CellChange,
    GameSaveLoad,
    GetCharactersWhoHearResponse,
}

// Payloads
public record PlayerAddedPayload(string PlayerId);
public record TargetChangedPayload(string PlayerId, string? NpcId);
public record CellChangePayload(string PlayerId, string CellName);
public record NearbyCharacterInfo(string CharacterId, float Distance);
public record GetCharactersWhoHearResponsePayload(NearbyCharacterInfo[] Characters);
