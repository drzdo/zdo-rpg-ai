namespace ZdoRpgAi.Server.Llm;

public enum LlmRole { User, Model }

public class LlmMessage {
    public required LlmRole Role { get; init; }
    public string? Text { get; init; }
    public List<LlmToolCall>? ToolCalls { get; init; }
    public List<LlmToolResult>? ToolResults { get; init; }
}

public class LlmToolCall {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Dictionary<string, object?> Arguments { get; init; } = new();
}

public class LlmToolResult {
    public required string CallId { get; init; }
    public required string Name { get; init; }
    public required string Result { get; init; }
}
