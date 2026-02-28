using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;

namespace ZdoRpgAi.Server.Llm.OpenAi;

public class OpenAiLlm : ILlm {
    private static readonly ILog Log = Logger.Get<OpenAiLlm>();
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly HttpClient _http = new();
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAiLlm(OpenAiConfig config) {
        _model = config.Model;
        _baseUrl = config.BaseUrl.TrimEnd('/');
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
    }

    public async Task<LlmResponse> ChatAsync(LlmRequest request) {
        var url = $"{_baseUrl}/v1/chat/completions";

        var body = new JsonObject {
            ["model"] = _model,
            ["messages"] = BuildMessages(request),
        };

        if (request.Tools.Count > 0) {
            body["tools"] = BuildTools(request.Tools);
        }

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

    private static JsonArray BuildMessages(LlmRequest request) {
        var messages = new JsonArray();

        var systemText = BuildSystemText(request);
        messages.AddNode(new JsonObject { ["role"] = "system", ["content"] = systemText });

        foreach (var msg in request.Messages) {
            if (msg.ToolResults != null) {
                foreach (var tr in msg.ToolResults) {
                    messages.AddNode(new JsonObject {
                        ["role"] = "tool",
                        ["tool_call_id"] = tr.CallId,
                        ["content"] = tr.Result,
                    });
                }
                continue;
            }

            var role = msg.Role == LlmRole.User ? "user" : "assistant";
            var obj = new JsonObject { ["role"] = role };

            if (msg.Text != null) {
                obj["content"] = msg.Text;
            }

            if (msg.ToolCalls is { Count: > 0 }) {
                var toolCalls = new JsonArray();
                foreach (var tc in msg.ToolCalls) {
                    var args = new JsonObject();
                    foreach (var kv in tc.Arguments) {
                        if (kv.Value is JsonElement je) {
                            args[kv.Key] = JsonNode.Parse(je.GetRawText());
                        }
                        else {
                            args[kv.Key] = kv.Value != null ? JsonValue.Create(kv.Value?.ToString()) : null;
                        }
                    }
                    toolCalls.AddNode(new JsonObject {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject {
                            ["name"] = tc.Name,
                            ["arguments"] = ToJson(args),
                        },
                    });
                }
                obj["tool_calls"] = toolCalls;
            }

            messages.AddNode(obj);
        }

        return messages;
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

    private static JsonArray BuildTools(List<LlmTool> tools) {
        var arr = new JsonArray();
        foreach (var tool in tools) {
            var parameters = new JsonObject { ["type"] = "object" };

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
                        foreach (var v in p.EnumValues) enumArr.AddNode(JsonValue.Create(v)!);
                        paramObj["enum"] = enumArr;
                    }
                    props[p.Name] = paramObj;
                    if (p.Required) {
                        required.AddNode(JsonValue.Create(p.Name)!);
                    }
                }
                parameters["properties"] = props;
                parameters["required"] = required;
            }

            arr.AddNode(new JsonObject {
                ["type"] = "function",
                ["function"] = new JsonObject {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = parameters,
                },
            });
        }
        return arr;
    }

    private static string ToJson(JsonNode node) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions)) {
            node.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static LlmResponse ParseResponse(string respJson) {
        var doc = JsonNode.Parse(respJson);
        var message = doc?["choices"]?[0]?["message"];

        if (message == null) {
            Log.Warn("Empty response (no choices)");
            return new LlmResponse { Text = null };
        }

        var text = message["content"]?.GetValue<string>();
        List<LlmToolCall>? toolCalls = null;

        var toolCallsNode = message["tool_calls"];
        if (toolCallsNode != null) {
            toolCalls = [];
            foreach (var tc in toolCallsNode.AsArray()) {
                if (tc == null) continue;
                var fn = tc["function"]!;
                var args = new Dictionary<string, object?>();
                var argsStr = fn["arguments"]?.GetValue<string>();
                if (argsStr != null) {
                    var argsNode = JsonNode.Parse(argsStr);
                    if (argsNode != null) {
                        foreach (var kv in argsNode.AsObject()) {
                            args[kv.Key] = kv.Value?.GetValue<string>();
                        }
                    }
                }
                toolCalls.Add(new LlmToolCall {
                    Id = tc["id"]!.GetValue<string>(),
                    Name = fn["name"]!.GetValue<string>(),
                    Arguments = args,
                });
            }
        }

        return new LlmResponse {
            Text = text,
            ToolCalls = toolCalls is { Count: > 0 } ? toolCalls : null,
        };
    }
}
