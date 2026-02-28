using System.Collections.Concurrent;
using KeraLua;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Server.Lua;

public class LuaSandbox {
    private static readonly ILog Log = Logger.Get<LuaSandbox>();

    private readonly ConcurrentQueue<KeraLua.Lua> _pool = new();
    private readonly int _poolSize;
    private readonly SemaphoreSlim _multiLock = new(1, 1);

    public LuaSandbox() {
        _poolSize = Environment.ProcessorCount;
        for (var i = 0; i < _poolSize; i++) {
            _pool.Enqueue(CreateState());
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
                _pool.TryDequeue(out var state);
                return state!;
            },
            (kvp, _, state) => {
                ApplyContext(state, ctx);
                try {
                    var status = state.LoadString($"return ({kvp.Value})");
                    if (status != LuaStatus.OK) {
                        Log.Error("Lua syntax error for {Id}: {Error}", kvp.Key, state.ToString(-1));
                        state.Pop(1);
                        return state;
                    }
                    status = state.PCall(0, 1, 0);
                    if (status != LuaStatus.OK) {
                        Log.Error("Lua runtime error for {Id}: {Error}", kvp.Key, state.ToString(-1));
                        state.Pop(1);
                        return state;
                    }
                    if (state.IsBoolean(-1) && state.ToBoolean(-1)) {
                        passed.Add(kvp.Key);
                    }
                    state.Pop(1);
                }
                catch (Exception ex) {
                    Log.Error("Lua condition error for {Id} ({Script}): {Error}", kvp.Key, kvp.Value, ex.Message);
                }
                return state;
            },
            state => _pool.Enqueue(state));

        return passed.ToList();
    }

    public bool EvalCondition(string luaCode, LuaContext ctx) {
        try {
            using var state = CreateState();
            ApplyContext(state, ctx);
            var status = state.LoadString($"return ({luaCode})");
            if (status != LuaStatus.OK) {
                Log.Error("Lua syntax error: {Error}", state.ToString(-1));
                return false;
            }
            status = state.PCall(0, 1, 0);
            if (status != LuaStatus.OK) {
                Log.Error("Lua runtime error: {Error}", state.ToString(-1));
                return false;
            }
            var result = state.IsBoolean(-1) && state.ToBoolean(-1);
            return result;
        }
        catch (Exception ex) {
            Log.Error("Lua condition error: {Error}", ex.Message);
            return false;
        }
    }

    public string? EvalContent(string luaCode, LuaContext ctx, Dictionary<string, object>? extraVars = null) {
        try {
            using var state = CreateState();
            ApplyContext(state, ctx);
            if (extraVars is { Count: > 0 }) {
                foreach (var (key, value) in extraVars) {
                    PushValue(state, value);
                    state.SetGlobal(key);
                }
            }
            var status = state.LoadString(luaCode);
            if (status != LuaStatus.OK) {
                var err = state.ToString(-1);
                state.Pop(1);
                throw new LuaException(err ?? "Syntax error");
            }
            status = state.PCall(0, 1, 0);
            if (status != LuaStatus.OK) {
                Log.Error("Lua runtime error: {Error}", state.ToString(-1));
                return null;
            }
            if (state.IsNil(-1) || state.IsNone(-1)) {
                return null;
            }
            return state.ToString(-1);
        }
        catch (LuaException) {
            throw;
        }
        catch (Exception ex) {
            Log.Error("Lua content error: {Error}", ex.Message);
            return null;
        }
    }

    public void ExecAction(string luaCode, LuaContext ctx, Dictionary<string, object?>? args = null) {
        try {
            using var state = CreateState();
            ApplyContext(state, ctx);
            if (args is { Count: > 0 }) {
                state.NewTable();
                foreach (var (key, value) in args) {
                    state.PushString(key);
                    PushValue(state, value);
                    state.SetTable(-3);
                }
                state.SetGlobal("args");
            }
            var status = state.LoadString(luaCode);
            if (status != LuaStatus.OK) {
                Log.Error("Lua syntax error: {Error}", state.ToString(-1));
                return;
            }
            status = state.PCall(0, 0, 0);
            if (status != LuaStatus.OK) {
                Log.Error("Lua runtime error: {Error}", state.ToString(-1));
            }
        }
        catch (Exception ex) {
            Log.Error("Lua action error: {Error}", ex.Message);
        }
    }

    private static KeraLua.Lua CreateState() {
        var state = new KeraLua.Lua();
        state.OpenLibs();
        // Remove dangerous libs for sandboxing
        state.PushNil();
        state.SetGlobal("os");
        state.PushNil();
        state.SetGlobal("io");
        state.PushNil();
        state.SetGlobal("loadfile");
        state.PushNil();
        state.SetGlobal("dofile");
        state.PushNil();
        state.SetGlobal("require");
        return state;
    }

    private static void ApplyContext(KeraLua.Lua state, LuaContext ctx) {
        state.NewTable();
        state.PushString("npcId");
        state.PushString(ctx.NpcId);
        state.SetTable(-3);
        state.PushString("playerId");
        state.PushString(ctx.PlayerId);
        state.SetTable(-3);
        state.SetGlobal("ctx");
    }

    private static void PushValue(KeraLua.Lua state, object? value) {
        switch (value) {
            case null:
                state.PushNil();
                break;
            case string s:
                state.PushString(s);
                break;
            case double d:
                state.PushNumber(d);
                break;
            case int i:
                state.PushNumber(i);
                break;
            case long l:
                state.PushNumber(l);
                break;
            case bool b:
                state.PushBoolean(b);
                break;
            default:
                state.PushString(value.ToString() ?? "");
                break;
        }
    }
}

public class LuaException : Exception {
    public LuaException(string message) : base(message) { }
}
