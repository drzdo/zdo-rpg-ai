namespace ZdoRpgAi.Server.Llm.OpenAi;

public class OpenAiConfig {
    public required string ApiKey { get; init; }
    public string Model { get; init; } = "gpt-4o";
    public string BaseUrl { get; init; } = "https://api.openai.com";
}
