using System.Text.Json;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Story;

public class StorySummaryBuilder {
    private readonly ILlm _simpleLlm;

    public StorySummaryBuilder(ILlm simpleLlm) {
        _simpleLlm = simpleLlm;
    }

    public async Task<string> SummarizeEventsAsync(List<StoryEvent> events) {
        var lines = new List<string>();
        foreach (var evt in events) {
            if (evt is StoryEvent.PlayerSpeak ps)
                lines.Add($"[{ps.GameTime}] {ps.PlayerCharacterId} says to {ps.TargetCharacterId ?? "nobody in particular"}: {ps.Text}");
            else if (evt is StoryEvent.NpcSpeak ns)
                lines.Add($"[{ns.GameTime}] {ns.NpcCharacterId} says to {ns.TargetCharacterId ?? "nobody in particular"}: {ns.Text}");
        }

        var request = new LlmRequest {
            SystemPrompt = "Summarize the following conversation events concisely, preserving key facts, decisions, and relationship changes. Write in past tense. Keep character IDs exactly as given.",
            Messages = [
                new LlmMessage {
                    Role = LlmRole.User,
                    Text = string.Join("\n", lines),
                },
            ],
        };

        var response = await _simpleLlm.ChatAsync(request);
        return response.Text?.Trim() ?? "Summary unavailable.";
    }

    public async Task<string> ConsolidateSummariesAsync(List<StoryEventSummary> summaries) {
        var text = string.Join("\n\n", summaries.Select((s, i) => $"Summary {i + 1}:\n{s.Summary}"));

        var request = new LlmRequest {
            SystemPrompt = "Consolidate the following summaries into a single cohesive summary. Preserve all key facts, decisions, and relationship changes. Write in past tense.",
            Messages = [
                new LlmMessage {
                    Role = LlmRole.User,
                    Text = text,
                },
            ],
        };

        var response = await _simpleLlm.ChatAsync(request);
        return response.Text?.Trim() ?? "Summary unavailable.";
    }
}
