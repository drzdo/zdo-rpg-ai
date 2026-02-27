namespace ZdoRpgAi.Server.Llm;

public interface ILlm {
    Task<LlmResponse> ChatAsync(LlmRequest request);
}

public class LlmRequest {
    public required string SystemPrompt { get; init; }
    public List<LlmMessage> Messages { get; init; } = [];
    public List<LlmTool> Tools { get; init; } = [];
    public List<LlmResource> Resources { get; init; } = [];
}

public class LlmResponse {
    public string? Text { get; init; }
    public List<LlmToolCall>? ToolCalls { get; init; }
}
