using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;

namespace ZdoRpgAi.ModEmulator.Console;

public class EmulatorSession {
    private static readonly ILog Log = Logger.Get<EmulatorSession>();

    private readonly RpcChannel _rpc;
    private string? _targetNpcId;

    public EmulatorSession(IChannel channel) {
        _rpc = new RpcChannel(channel);
        _rpc.MessageReceived += OnMessageReceived;
        _rpc.Disconnected += OnDisconnected;
    }

    public async Task RunAsync(CancellationToken ct) {
        // Send initial game state to client
        SendPlayerAdded();
        SendCellChange();

        // Default target: Fargoth
        SetTarget(SeydaNeenWorld.Npcs[0].ObjectId);

        using var reg = ct.Register(() => _rpc.Close());
        await _rpc.RunAsync();
    }

    public void SendPlayerSpeaksText(string text) {
        var payload = JsonExtensions.SerializeToObject(
            new PlayerSpeaksTextPayload(SeydaNeenWorld.PlayerId, text, _targetNpcId, "0"),
            PayloadJsonContext.Default.PlayerSpeaksTextPayload);
        _rpc.Publish(nameof(ClientToServerMessageType.PlayerSpeaksText), payload);
        Log.Info("Player says: \"{Text}\" (target: {Target})", text, _targetNpcId ?? "(none)");
    }

    public void SetTarget(string? npcId) {
        _targetNpcId = npcId;
        var payload = JsonExtensions.SerializeToObject(
            new TargetChangedPayload(SeydaNeenWorld.PlayerId, npcId),
            PayloadJsonContext.Default.TargetChangedPayload);
        _rpc.Publish(nameof(ModToServerMessageType.TargetChanged), payload);
        if (npcId != null) {
            Log.Info("Target set to: {NpcId}", npcId);
        } else {
            Log.Info("Target cleared");
        }
    }

    private void SendPlayerAdded() {
        var payload = JsonExtensions.SerializeToObject(
            new PlayerAddedPayload(SeydaNeenWorld.PlayerId),
            PayloadJsonContext.Default.PlayerAddedPayload);
        _rpc.Publish(nameof(ModToServerMessageType.PlayerAdded), payload);
        Log.Info("Sent PlayerAdded");
    }

    private void SendCellChange() {
        var payload = JsonExtensions.SerializeToObject(
            new CellChangePayload(SeydaNeenWorld.PlayerId, SeydaNeenWorld.CellName),
            PayloadJsonContext.Default.CellChangePayload);
        _rpc.Publish(nameof(ModToServerMessageType.CellChange), payload);
        Log.Info("Sent CellChange: {Cell}", SeydaNeenWorld.CellName);
    }

    private void OnMessageReceived(Message msg) {
        Log.Debug("Received: {Type} (id={Id})", msg.Type, msg.Id);

        switch (msg.Type) {
            case nameof(ServerToModMessageType.GetNpcInfo):
                HandleGetNpcInfo(msg);
                break;
            case nameof(ServerToModMessageType.GetPlayerInfo):
                HandleGetPlayerInfo(msg);
                break;
            case nameof(ServerToModMessageType.GetCharactersWhoHear):
                HandleGetCharactersWhoHear(msg);
                break;
            case nameof(ServerToModMessageType.SpeechRecognitionInProgress):
                HandleSpeechRecognitionInProgress(msg);
                break;
            case nameof(ServerToModMessageType.SpeechRecognitionComplete):
                HandleSpeechRecognitionComplete(msg);
                break;
            case nameof(ServerToModMessageType.SpawnOnGroundInFrontOfCharacter):
                HandleSpawnItem(msg);
                break;
            case nameof(ServerToModMessageType.PlaySound3dOnCharacter):
                HandlePlaySound(msg);
                break;
            case nameof(ServerToModMessageType.NpcStartFollowCharacter):
                HandleNpcStartFollow(msg);
                break;
            case nameof(ServerToModMessageType.NpcStopFollowCharacter):
                HandleNpcStopFollow(msg);
                break;
            case nameof(ServerToModMessageType.NpcAttack):
                HandleNpcAttack(msg);
                break;
            case nameof(ServerToModMessageType.NpcStopAttack):
                HandleNpcStopAttack(msg);
                break;
            case nameof(ServerToModMessageType.ShowMessageBox):
                HandleShowMessageBox(msg);
                break;
            case nameof(ClientToModMessageType.SayMp3File):
                HandleSayMp3File(msg);
                break;
            case nameof(ClientToBothMessageType.PlayerStartSpeak):
                Log.Info("[MOD] Player started speaking");
                break;
            case nameof(ClientToBothMessageType.PlayerStopSpeak):
                Log.Info("[MOD] Player stopped speaking");
                break;
            default:
                Log.Warn("Unhandled message type: {Type}", msg.Type);
                break;
        }
    }

    private void HandleGetNpcInfo(Message msg) {
        var req = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.GetNpcInfoRequestPayload);
        if (req == null) return;

        var npc = SeydaNeenWorld.FindNpc(req.NpcId);
        if (npc == null) {
            Log.Warn("NPC not found: {NpcId}", req.NpcId);
            var empty = new JsonObject();
            _rpc.Respond(nameof(ServerToModMessageType.GetNpcInfo), msg.Id, empty);
            return;
        }

        var response = SeydaNeenWorld.ToNpcInfo(npc);
        var payload = JsonExtensions.SerializeToObject(response, PayloadJsonContext.Default.GetNpcInfoResponsePayload);
        _rpc.Respond(nameof(ServerToModMessageType.GetNpcInfo), msg.Id, payload);
        Log.Info("GetNpcInfo: {Name} ({Race}, {Sex})", npc.Name, npc.Race, npc.Sex);
    }

    private void HandleGetPlayerInfo(Message msg) {
        var info = SeydaNeenWorld.PlayerInfo;
        var payload = JsonExtensions.SerializeToObject(info, PayloadJsonContext.Default.GetPlayerInfoResponsePayload);
        _rpc.Respond(nameof(ServerToModMessageType.GetPlayerInfo), msg.Id, payload);
        Log.Info("GetPlayerInfo: {Name}", info.Name);
    }

    private void HandleGetCharactersWhoHear(Message msg) {
        var req = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload);
        if (req == null) return;

        var characters = SeydaNeenWorld.GetCharactersWhoHear(req.CharacterId, req.MaxDistanceMeters);
        var response = new GetCharactersWhoHearResponsePayload(characters);
        var payload = JsonExtensions.SerializeToObject(response, PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload);
        _rpc.Respond(nameof(ModToServerMessageType.GetCharactersWhoHearResponse), msg.Id, payload);
        Log.Info("GetCharactersWhoHear ({CharacterId}): {Count} nearby", req.CharacterId, characters.Length);
    }

    private void HandleSpeechRecognitionInProgress(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SpeechRecognitionInProgressPayload);
        if (p != null) {
            Log.Info("[MOD] Speech recognition interim: '{Text}'", p.Text);
        }
    }

    private void HandleSpeechRecognitionComplete(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SpeechRecognitionCompletePayload);
        if (p != null) {
            Log.Info("[MOD] Speech recognition final: '{Text}'", p.Text);
        }
    }

    private void HandleSpawnItem(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SpawnOnGroundInFrontOfCharacterPayload);
        if (p != null) {
            Log.Info("[MOD] Spawn item '{ItemId}' x{Count} in front of {NpcId}", p.ItemId, p.Count, p.NpcId);
        }
    }

    private void HandlePlaySound(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PlaySound3dOnCharacterPayload);
        if (p != null) {
            Log.Info("[MOD] Play sound '{Sound}' on {NpcId}", p.Sound, p.NpcId);
        }
    }

    private void HandleNpcStartFollow(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcStartFollowCharacterPayload);
        if (p != null) {
            Log.Info("[MOD] {NpcId} starts following {Target}", p.NpcId, p.TargetCharacterId);
        }
    }

    private void HandleNpcStopFollow(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcStopFollowCharacterPayload);
        if (p != null) {
            Log.Info("[MOD] {NpcId} stops following", p.NpcId);
        }
    }

    private void HandleNpcAttack(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcAttackPayload);
        if (p != null) {
            Log.Info("[MOD] {NpcId} attacks {Target}", p.NpcId, p.TargetCharacterId);
        }
    }

    private void HandleNpcStopAttack(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.NpcStopAttackPayload);
        if (p != null) {
            Log.Info("[MOD] {NpcId} stops attacking", p.NpcId);
        }
    }

    private void HandleShowMessageBox(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.ShowMessageBoxPayload);
        if (p != null) {
            Log.Info("[MOD] MessageBox: {Message}", p.Message);
        }
    }

    private void HandleSayMp3File(Message msg) {
        var p = msg.Json?.DeserializeSafe(PayloadJsonContext.Default.SayMp3FilePayload);
        if (p != null) {
            var npc = SeydaNeenWorld.FindNpc(p.NpcId);
            var name = npc?.Name ?? p.NpcId;
            Log.Info("[MOD] {Name} says: \"{Text}\" (audio: {Mp3}, duration: {Duration:F1}s)",
                name, p.Text, p.Mp3Name, p.DurationSec ?? 0);
        }
    }

    private void OnDisconnected() {
        Log.Info("Client disconnected");
    }
}
