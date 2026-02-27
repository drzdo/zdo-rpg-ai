using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Server.Llm.Gemini;

public class GeminiLlm : ILlm {
    private static readonly ILog Log = Logger.Get<GeminiLlm>();
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly HttpClient _http = new();
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _thinkingBudget;

    public GeminiLlm(GeminiConfig config) {
        _apiKey = config.ApiKey;
        _model = config.Model;
        _thinkingBudget = config.ThinkingBudget;
    }

    public async Task<LlmResponse> ChatAsync(LlmRequest request) {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var systemText = BuildSystemText(request);
        var body = new JsonObject {
            ["systemInstruction"] = new JsonObject {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = systemText } }
            },
            ["contents"] = BuildContents(request.Messages),
        };

        if (request.Tools.Count > 0) {
            body["tools"] = BuildTools(request.Tools);
        }

        body["generationConfig"] = new JsonObject {
            ["thinkingConfig"] = new JsonObject {
                ["thinkingBudget"] = _thinkingBudget
            }
        };

        var json = ToJson(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Log.Debug("Sending request ({MessageCount} messages, {ToolCount} tools, {ResourceCount} resources)",
            request.Messages.Count, request.Tools.Count, request.Resources.Count);
        Log.Trace("Request body: {Body}", json);

        var resp = await _http.PostAsync(url, content);
        var respJson = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode) {
            Log.Error("API error {StatusCode}: {Response}", resp.StatusCode, respJson);
            return new LlmResponse { Text = "[LLM error]" };
        }

        Log.Trace("Raw response: {Response}", respJson);

        return ParseResponse(respJson);
    }

    private static string BuildSystemText(LlmRequest request) {
        if (request.Resources.Count == 0) {
            return request.SystemPrompt;
        }

        var sb = new StringBuilder(request.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("# Available Resources");
        foreach (var resource in request.Resources) {
            sb.AppendLine();
            sb.AppendLine($"## {resource.Name}");
            if (!string.IsNullOrEmpty(resource.Description)) {
                sb.AppendLine(resource.Description);
            }
            sb.AppendLine();
            sb.AppendLine(resource.Content);
        }
        return sb.ToString();
    }

    private static JsonArray BuildContents(List<LlmMessage> messages) {
        var contents = new JsonArray();
        foreach (var msg in messages) {
            var parts = new JsonArray();

            if (msg.Text != null) {
                parts.Add(new JsonObject { ["text"] = msg.Text });
            }

            if (msg.ToolCalls != null) {
                foreach (var tc in msg.ToolCalls) {
                    var args = new JsonObject();
                    foreach (var kv in tc.Arguments) {
                        if (kv.Value is JsonElement je) {
                            args[kv.Key] = JsonNode.Parse(je.GetRawText());
                        }
                        else {
                            args[kv.Key] = kv.Value != null ? JsonValue.Create(kv.Value) : null;
                        }
                    }
                    parts.Add(new JsonObject {
                        ["functionCall"] = new JsonObject { ["name"] = tc.Name, ["args"] = args }
                    });
                }
            }

            if (msg.ToolResults != null) {
                foreach (var tr in msg.ToolResults) {
                    parts.Add(new JsonObject {
                        ["functionResponse"] = new JsonObject {
                            ["name"] = tr.Name,
                            ["response"] = new JsonObject { ["result"] = tr.Result }
                        }
                    });
                }
            }

            var role = msg.Role == LlmRole.User ? "user" : "model";
            contents.Add(new JsonObject { ["role"] = role, ["parts"] = parts });
        }
        return contents;
    }

    private static JsonArray BuildTools(List<LlmTool> tools) {
        var funcDecls = new JsonArray();
        foreach (var tool in tools) {
            var decl = new JsonObject {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
            };

            if (tool.Parameters.Count > 0) {
                var props = new JsonObject();
                var required = new JsonArray();
                foreach (var p in tool.Parameters) {
                    var paramObj = new JsonObject {
                        ["type"] = p.Type,
                        ["description"] = p.Description,
                    };
                    if (p.EnumValues is { Count: > 0 }) {
                        var enumArr = new JsonArray();
                        foreach (var v in p.EnumValues) enumArr.Add(v);
                        paramObj["enum"] = enumArr;
                    }
                    props[p.Name] = paramObj;
                    if (p.Required) {
                        required.Add(p.Name);
                    }
                }
                decl["parameters"] = new JsonObject {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = required,
                };
            }

            funcDecls.Add(decl);
        }
        return new JsonArray { new JsonObject { ["functionDeclarations"] = funcDecls } };
    }

    private static string ToJson(JsonObject body) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions)) {
            body.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static LlmResponse ParseResponse(string respJson) {
        var doc = JsonNode.Parse(respJson);
        var parts = doc?["candidates"]?[0]?["content"]?["parts"];

        if (parts == null) {
            var blockReason = doc?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            if (blockReason != null) {
                Log.Warn("Response blocked: {Reason}", blockReason);
            }
            else {
                Log.Warn("Empty response (no candidates)");
            }
            return new LlmResponse { Text = null };
        }

        string? text = null;
        var toolCalls = new List<LlmToolCall>();
        var callIndex = 0;

        foreach (var part in parts.AsArray()) {
            if (part?["text"] != null) {
                text = (text ?? "") + part["text"]!.GetValue<string>();
            }

            if (part?["functionCall"] != null) {
                var fc = part["functionCall"]!;
                var args = new Dictionary<string, object?>();
                if (fc["args"] != null) {
                    foreach (var kv in fc["args"]!.AsObject()) {
                        args[kv.Key] = kv.Value?.GetValue<string>();
                    }
                }
                toolCalls.Add(new LlmToolCall {
                    Id = $"call_{callIndex++}",
                    Name = fc["name"]!.GetValue<string>(),
                    Arguments = args,
                });
            }
        }

        return new LlmResponse {
            Text = text,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
        };
    }
}
