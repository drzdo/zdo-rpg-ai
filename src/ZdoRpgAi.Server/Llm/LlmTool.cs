namespace ZdoRpgAi.Server.Llm;

public class LlmTool {
    public required string Name { get; init; }
    public required string Description { get; init; }
    public List<LlmToolParameter> Parameters { get; init; } = [];
}

public class LlmToolParameter {
    public required string Name { get; init; }
    public string Type { get; init; } = "string";
    public required string Description { get; init; }
    public bool Required { get; init; } = true;
    public List<string>? EnumValues { get; init; }
}
