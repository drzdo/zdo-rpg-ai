using System.Text.Json.Nodes;

namespace ZdoRpgAi.Protocol.Channel;

public record Message(string Type, int Id, int? ResponseTo, JsonObject? Json, byte[]? Binary) {
    public JsonObject ToJson() {
        var obj = new JsonObject();
        obj["id"] = Id;
        obj["type"] = Type;
        if (ResponseTo.HasValue) {
            obj["responseTo"] = ResponseTo.Value;
        }

        if (Json != null) {
            obj["data"] = Json.DeepClone();
        }

        return obj;
    }

    public static Message FromJson(JsonObject obj, byte[]? binary = null) {
        var type = obj["type"]?.GetValue<string>() ?? "";
        var id = obj["id"]?.GetValue<int>() ?? 0;
        var responseTo = obj["responseTo"]?.GetValue<int>();
        var data = obj["data"]?.AsObject();
        return new Message(type, id, responseTo, data, binary);
    }
}
