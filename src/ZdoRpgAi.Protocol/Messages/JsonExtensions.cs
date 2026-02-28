using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Protocol.Messages;

public static class JsonExtensions {
    private static readonly ILog Log = Logger.Get<PayloadJsonContext>();

    public static T? DeserializeSafe<T>(this JsonNode json, JsonTypeInfo<T> typeInfo) {
        try {
            return json.Deserialize(typeInfo);
        }
        catch (Exception ex) {
            Log.Error("Failed to deserialize {Type}: {Error}", typeof(T).Name, ex.Message);
            return default;
        }
    }

    public static JsonObject SerializeToObject<T>(T value, JsonTypeInfo<T> typeInfo) =>
        (JsonObject)JsonSerializer.SerializeToNode(value, typeInfo)!;

    public static void AddNode(this JsonArray array, JsonNode node) => array.Add(node);
}
