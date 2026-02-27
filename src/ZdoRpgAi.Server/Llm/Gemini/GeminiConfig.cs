namespace ZdoRpgAi.Server.Llm.Gemini;

public class GeminiConfig {
    public required string ApiKey { get; init; }
    public string Model { get; init; } = "gemini-2.5-flash";
    public int ThinkingBudget { get; init; }
}
