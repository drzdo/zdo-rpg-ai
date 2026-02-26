namespace ZdoRpgAi.Util;

public class CommandLineArgsParser {
    private readonly string _appName;
    private readonly string _version;
    private readonly List<ArgDef> _args = new();

    public CommandLineArgsParser(string appName, string version) {
        _appName = appName;
        _version = version;
    }

    public void Add(string name, string description, string? defaultValue = null) {
        _args.Add(new ArgDef(null, name, description, defaultValue));
    }

    public void Add(string shortName, string longName, string description, string? defaultValue = null) {
        _args.Add(new ArgDef(shortName, longName, description, defaultValue));
    }

    public ParsedArgs Parse(string[] args) {
        var values = new Dictionary<string, string>();
        var matched = new HashSet<string>();

        for (var i = 0; i < args.Length; i++) {
            var arg = args[i];

            if (arg is "--help" or "-h") {
                PrintHelp(Console.Out);
                Environment.Exit(0);
            }
            if (arg is "--version" or "-v") {
                Console.WriteLine($"{_appName} {_version}");
                Environment.Exit(0);
            }

            // --key=value
            string? key;
            string? value;
            var eqIdx = arg.IndexOf('=');
            if (eqIdx >= 0) {
                key = arg[..eqIdx];
                value = arg[(eqIdx + 1)..];
            }
            else {
                key = arg;
                value = null;
            }

            var def = FindDef(key);
            if (def == null) {
                Console.Error.WriteLine($"Unknown argument: {key}");
                Console.Error.WriteLine();
                PrintHelp(Console.Error);
                Environment.Exit(1);
            }

            if (value == null) {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-')) {
                    value = args[++i];
                }
            }

            if (value == null) {
                Console.Error.WriteLine($"Missing value for {key}");
                Environment.Exit(1);
            }

            values[def.LongName] = value;
            matched.Add(def.LongName);
        }

        // Apply defaults for unmatched args
        foreach (var def in _args) {
            if (matched.Contains(def.LongName)) continue;
            if (def.DefaultValue != null) {
                values[def.LongName] = def.DefaultValue;
            }
        }

        return new ParsedArgs(values);
    }

    private ArgDef? FindDef(string key) {
        foreach (var def in _args) {
            if (def.LongName == key || def.ShortName == key) return def;
        }
        return null;
    }

    private void PrintHelp(TextWriter writer) {
        writer.WriteLine($"Usage: {_appName} [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");

        var entries = new List<(string names, string desc)> {
            ("-h, --help", "Show help"),
            ("-v, --version", "Show version"),
        };
        foreach (var def in _args) {
            var names = def.ShortName != null ? $"{def.ShortName}, {def.LongName}" : $"    {def.LongName}";
            var desc = def.Description;
            if (def.DefaultValue != null) desc += $" (default: {def.DefaultValue})";
            entries.Add((names, desc));
        }

        var maxNames = 0;
        foreach (var e in entries) {
            if (e.names.Length > maxNames) maxNames = e.names.Length;
        }

        foreach (var (names, desc) in entries) {
            writer.WriteLine($"  {names.PadRight(maxNames)}  {desc}");
        }
    }

    private record ArgDef(string? ShortName, string LongName, string Description, string? DefaultValue);
}

public class ParsedArgs {
    private readonly Dictionary<string, string> _values;

    internal ParsedArgs(Dictionary<string, string> values) {
        _values = values;
    }

    public string? Get(string name) {
        return _values.TryGetValue(name, out var value) ? value : null;
    }
}
