using System.Collections.Concurrent;
using MoonSharp.Interpreter;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Server.Lua;

public class LuaSandbox {
    private static readonly ILog Log = Logger.Get<LuaSandbox>();

    private readonly ConcurrentQueue<Script> _pool = new();
    private readonly int _poolSize;
    private readonly SemaphoreSlim _multiLock = new(1, 1);

    public LuaSandbox() {
        UserData.RegisterType<LuaContext>();
        _poolSize = Environment.ProcessorCount;
        for (var i = 0; i < _poolSize; i++) {
            _pool.Enqueue(new Script(CoreModules.Preset_HardSandbox));
        }
    }

    public List<string> EvalConditionMulti(Dictionary<string, string> conditions, LuaContext ctx) {
        _multiLock.Wait();
        try {
            return EvalConditionMultiInner(conditions, ctx);
        }
        finally {
            _multiLock.Release();
        }
    }

    private List<string> EvalConditionMultiInner(Dictionary<string, string> conditions, LuaContext ctx) {
        var passed = new ConcurrentBag<string>();

        Parallel.ForEach(
            conditions,
            new ParallelOptions { MaxDegreeOfParallelism = _poolSize },
            () => {
                _pool.TryDequeue(out var script);
                ApplyContext(script!, ctx);
                return script!;
            },
            (kvp, _, script) => {
                try {
                    var result = script.DoString($"return ({kvp.Value})");
                    if (result.Type == DataType.Boolean && result.Boolean) {
                        passed.Add(kvp.Key);
                    }
                }
                catch (Exception ex) {
                    Log.Error("Lua condition error for {Id} ({Script}): {Error}", kvp.Key, kvp.Value, ex.Message);
                }
                return script;
            },
            script => _pool.Enqueue(script));

        return passed.ToList();
    }

    public bool EvalCondition(string luaCode, LuaContext ctx) {
        try {
            var script = CreateScript(ctx);
            var result = script.DoString($"return ({luaCode})");
            return result.Type == DataType.Boolean && result.Boolean;
        }
        catch (Exception ex) {
            Log.Error("Lua condition error: {Error}", ex.Message);
            return false;
        }
    }

    public string? EvalContent(string luaCode, LuaContext ctx, Dictionary<string, object>? extraVars = null) {
        try {
            var script = CreateScript(ctx);
            if (extraVars is { Count: > 0 }) {
                foreach (var (key, value) in extraVars) {
                    script.Globals[key] = value switch {
                        string s => DynValue.NewString(s),
                        double d => DynValue.NewNumber(d),
                        int i => DynValue.NewNumber(i),
                        long l => DynValue.NewNumber(l),
                        bool b => DynValue.NewBoolean(b),
                        _ => DynValue.NewString(value.ToString() ?? ""),
                    };
                }
            }
            var result = script.DoString(luaCode);
            if (result.IsNil() || result.IsVoid()) {
                return null;
            }
            return result.Type == DataType.String ? result.String : result.ToPrintString();
        }
        catch (SyntaxErrorException) {
            throw;
        }
        catch (Exception ex) {
            Log.Error("Lua content error: {Error}", ex.Message);
            return null;
        }
    }

    public void ExecAction(string luaCode, LuaContext ctx, Dictionary<string, object?>? args = null) {
        try {
            var script = CreateScript(ctx);
            if (args is { Count: > 0 }) {
                var table = new Table(script);
                foreach (var (key, value) in args) {
                    table[key] = value switch {
                        string s => DynValue.NewString(s),
                        double d => DynValue.NewNumber(d),
                        int i => DynValue.NewNumber(i),
                        long l => DynValue.NewNumber(l),
                        bool b => DynValue.NewBoolean(b),
                        null => DynValue.Nil,
                        _ => DynValue.NewString(value.ToString() ?? ""),
                    };
                }
                script.Globals["args"] = table;
            }
            script.DoString(luaCode);
        }
        catch (Exception ex) {
            Log.Error("Lua action error: {Error}", ex.Message);
        }
    }

    private Script CreateScript(LuaContext ctx) {
        var script = new Script(CoreModules.Preset_HardSandbox);
        ApplyContext(script, ctx);
        return script;
    }

    private static void ApplyContext(Script script, LuaContext ctx) {
        script.Globals["ctx"] = UserData.Create(ctx);
    }
}
