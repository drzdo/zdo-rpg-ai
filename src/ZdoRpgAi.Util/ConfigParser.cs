using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ZdoRpgAi.Util;

public static class ConfigParser {
    public static T FromFile<T>(string path, JsonTypeInfo<T> jsonTypeInfo) {
        var fullPath = Path.GetFullPath(path);
        string json;
        try {
            json = File.ReadAllText(fullPath);
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed to read config: {fullPath} ({ex.Message})");
            Environment.Exit(1);
            return default!; // unreachable
        }

        try {
            var config = JsonSerializer.Deserialize(json, jsonTypeInfo);
            if (config == null) {
                Console.Error.WriteLine($"Failed to parse config: {fullPath}");
                Environment.Exit(1);
            }
            return config;
        }
        catch (JsonException ex) {
            Console.Error.WriteLine($"Invalid JSON in config: {fullPath} ({ex.Message})");
            Environment.Exit(1);
            return default!; // unreachable
        }
    }
}
