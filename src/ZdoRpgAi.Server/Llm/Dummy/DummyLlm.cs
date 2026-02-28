namespace ZdoRpgAi.Server.Llm.Dummy;

public class DummyLlm : ILlm {
    public Task<LlmResponse> ChatAsync(LlmRequest request) =>
        Task.FromResult(new LlmResponse { Text = "This is dummy response" });
}
