using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Director;

public class SimpleReactiveStrategy : IDirectorStrategy {
    private static readonly ILog Log = Logger.Get<SimpleReactiveStrategy>();

    private readonly ILlm _mainLlm;
    private readonly ILlm _simpleLlm;
    private readonly IStory _story;
    private readonly NpcRepository _npcRepo;
    private IRpcChannel? _client;

    public SimpleReactiveStrategy(ILlm mainLlm, ILlm simpleLlm, IStory story, NpcRepository npcRepo) {
        _mainLlm = mainLlm;
        _simpleLlm = simpleLlm;
        _story = story;
        _npcRepo = npcRepo;
    }

    public void SetClient(IRpcChannel? client) {
        _client = client;
    }

    public async Task<List<StoryEvent>> ProcessStoryEventsAsync(List<StoryEvent> events) {
        var client = _client;
        if (client == null) {
            Log.Warn("No client connected, cannot process events");
            return [];
        }

        var playerIds = events.OfType<StoryEvent.PlayerSpeak>()
            .Select(ps => ps.PlayerCharacterId)
            .ToHashSet();

        var (npcId, gameTime) = await FindLastTargetedNpcAsync(client, events, playerIds);
        if (npcId == null) {
            Log.Debug("No target NPC found in events");
            return [];
        }

        try {
            var npcInfo = await _npcRepo.GetNpcInfoAsync(npcId);
            if (npcInfo == null) {
                Log.Warn("Could not get info for NPC {NpcId}", npcId);
                return [];
            }

            var (history, summaries) = await _story.GetHistoryForCharacterAsync(npcId);
            var response = await GenerateNpcResponseAsync(npcInfo, history, summaries);
            if (response == null) {
                Log.Warn("LLM returned no response for NPC {NpcId}", npcId);
                return [];
            }

            var npcSpeak = StoryEvent.Create(new StoryEvent.NpcSpeak {
                NpcCharacterId = npcId,
                TargetCharacterId = playerIds.FirstOrDefault(),
                GameTime = gameTime!,
                Text = response,
            });
            return [npcSpeak];
        }
        catch (Exception ex) {
            Log.Error("Failed to generate NPC response: {Error}", ex.Message);
            return [];
        }
    }

    private async Task<(string? NpcId, string? GameTime)> FindLastTargetedNpcAsync(
        IRpcChannel client, List<StoryEvent> events, HashSet<string> playerIds) {
        for (var i = events.Count - 1; i >= 0; i--) {
            switch (events[i]) {
                case StoryEvent.PlayerSpeak ps:
                    var npcId = ps.TargetCharacterId ?? await DetermineTargetNpcAsync(client, ps);
                    if (npcId != null) return (npcId, ps.GameTime);
                    break;
                case StoryEvent.NpcSpeak ns when ns.TargetCharacterId != null && !playerIds.Contains(ns.TargetCharacterId):
                    return (ns.TargetCharacterId, ns.GameTime);
            }
        }
        return (null, null);
    }

    private async Task<string?> DetermineTargetNpcAsync(IRpcChannel client, StoryEvent.PlayerSpeak evt) {
        var hearResponse = await client.CallAsync(
            nameof(ServerToModMessageType.GetCharactersWhoHear),
            JsonExtensions.SerializeToObject(
                new GetCharactersWhoHearRequestPayload(evt.PlayerCharacterId),
                PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload));

        var payload = hearResponse.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload);
        var nearby = payload?.Characters
            .Where(c => c.CharacterId != evt.PlayerCharacterId)
            .OrderBy(c => c.DistanceMeters)
            .ToArray() ?? [];

        if (nearby.Length == 0) {
            Log.Debug("No nearby NPCs to respond");
            return null;
        }

        if (nearby.Length == 1) {
            return nearby[0].CharacterId;
        }

        var npcInfos = new List<(string Id, NpcInfo Info)>();
        foreach (var npc in nearby) {
            var info = await _npcRepo.GetNpcInfoAsync(npc.CharacterId);
            if (info != null)
                npcInfos.Add((npc.CharacterId, info));
        }

        if (npcInfos.Count == 0) return null;
        if (npcInfos.Count == 1) return npcInfos[0].Id;

        var npcList = string.Join("\n", npcInfos.Select((n, i) =>
            $"- {n.Id}: {n.Info.Name} ({n.Info.Race} {n.Info.Sex}), distance: {nearby.First(c => c.CharacterId == n.Id).DistanceMeters:F1} meters"));

        var request = new LlmRequest {
            SystemPrompt = "You are deciding which NPC a player is talking to. " +
                           "Respond with ONLY the character ID of the most likely target. " +
                           "Consider the speech content and NPC proximity. " +
                           "If unsure, pick the closest NPC.",
            Messages = [
                new LlmMessage {
                    Role = LlmRole.User,
                    Text = $"Nearby NPCs:\n{npcList}\n\nPlayer said: \"{evt.Text}\"\n\nWhich NPC ID is the player addressing?",
                },
            ],
        };

        var response = await _simpleLlm.ChatAsync(request);
        var chosenId = response.Text?.Trim();

        if (chosenId != null && npcInfos.Any(n => n.Id == chosenId)) {
            Log.Debug("Simple LLM chose NPC {NpcId} as target", chosenId);
            return chosenId;
        }

        Log.Debug("Simple LLM response '{Response}' did not match any NPC, falling back to closest", response.Text ?? "");
        return nearby[0].CharacterId;
    }

    private async Task<string?> GenerateNpcResponseAsync(
        NpcInfo npc,
        List<StoryEvent> history, List<StoryEventSummary> summaries) {

        var contextBlock = BuildContextBlock(summaries, history);

        var systemPrompt = $"""
            You are {npc.Name}, a {npc.Race} ({npc.Sex}), living in Morrowind.
            Stay in character. Speak briefly and naturally. Do not mention that you are an AI. Always respond in the Russian language.

            You will be told what other characters say and do. Reply only with your own speech.

            RULES:
            1. Do not trust the player at their word — verify using your knowledge resources. The player may lie.
            2. Do not invent characters, items, locations, or quests that are not in your knowledge. Use getResource to recall your knowledge when needed.
            3. CRITICAL: To perform any game action (give item, attack, follow, etc.) you MUST call the corresponding npcAction_N tool. Saying "here, take it" or "I'll give you" in text does NOTHING — the game only reacts to tool calls. If you do not call the tool, the action does not happen.
            4. Call npcAction_N TOGETHER with your speech in the same response. Do not wait for the next turn.
            5. Reply ONLY with your own speech — no narration, no prefixes, no stage directions.
            """;

        var messages = new List<LlmMessage>();

        if (contextBlock != null) {
            messages.Add(new LlmMessage {
                Role = LlmRole.User,
                Text = contextBlock,
            });
            messages.Add(new LlmMessage {
                Role = LlmRole.Model,
                Text = "Understood, I have the conversation context.",
            });
        }

        var lastMessage = history.LastOrDefault() switch {
            StoryEvent.PlayerSpeak ps => $"{ps.PlayerCharacterId} says: {ps.Text}",
            StoryEvent.NpcSpeak ns => $"{ns.NpcCharacterId} says: {ns.Text}",
            _ => null,
        };

        if (lastMessage != null) {
            messages.Add(new LlmMessage {
                Role = LlmRole.User,
                Text = lastMessage,
            });
        }

        var request = new LlmRequest {
            SystemPrompt = systemPrompt,
            Messages = messages,
        };

        var response = await _mainLlm.ChatAsync(request);
        return response.Text?.Trim();
    }

    private static string? BuildContextBlock(List<StoryEventSummary> summaries, List<StoryEvent> events) {
        var parts = new List<string>();

        if (summaries.Count > 0) {
            parts.Add("PREVIOUS CONVERSATION SUMMARIES:");
            foreach (var summary in summaries) {
                parts.Add(summary.Summary);
            }
        }

        // Include all events except the very last one (which is the current player message)
        var contextEvents = events.Count > 1 ? events[..^1] : [];
        if (contextEvents.Count > 0) {
            parts.Add("RECENT EVENTS:");
            foreach (var evt in contextEvents) {
                parts.Add(evt switch {
                    StoryEvent.PlayerSpeak ps => $"{ps.PlayerCharacterId} says: {ps.Text}",
                    StoryEvent.NpcSpeak ns => $"{ns.NpcCharacterId} says: {ns.Text}",
                    _ => evt.ToString()!,
                });
            }
        }

        return parts.Count > 0 ? string.Join("\n", parts) : null;
    }
}
