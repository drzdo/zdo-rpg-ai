# C# Conventions

- **Events**: Use past tense verb without `On` prefix (e.g. `Disconnected`, `MessageReceived`, `KeyPressed`). The `On` prefix is reserved for the method that raises the event.
- **Async methods**: Suffix with `Async` for methods returning `Task`/`Task<T>` (e.g. `RunAsync`).
- **Logging**: Use `ZdoRpgAi.Core.ILog` via `Logger.Get<T>()`. Method names: `Trace`, `Debug`, `Info`, `Warn`, `Error`. Do not reference Serilog directly outside of Core. Prefer logging a warning over silently returning early — if a guard clause skips work due to unexpected state, log `Warn` so the issue is visible.
- **No XML doc comments** (`<summary>`) unless they add value beyond what the name already communicates.
- **Native AOT**: Do not use features or libraries that are incompatible with native AOT compilation (e.g. unconstrained reflection, `dynamic`, non-trimming-safe APIs).
- **Repository methods**: Accept and return typed model classes, not raw JSON strings. JSON serialization/deserialization is the repository implementation's responsibility.
- **Enum string values**: When storing or comparing enum values as strings, use `nameof()` (e.g. `nameof(ConversationEntryType.Speak)`) — no lowercasing or manual string literals.
- **Client config**: YAML format (`example/client-config.example.yaml`). Parsed via YamlDotNet → `System.Text.Json` source generation for AOT compatibility. The `JsonSerializerContext` is at `src/ZdoRpgAi.Client/Bootstrap/ClientConfigJsonContext.cs`. Only the root type (`ClientConfig`) needs `[JsonSerializable]` — nested types are discovered automatically. When changing `ClientConfig` or its nested types, update `example/client-config.example.yaml` to match.
- **Server config**: YAML format (`example/server-config.example.yaml`). Config classes at `src/ZdoRpgAi.Server/Bootstrap/ServerConfig.cs`. `JsonSerializerContext` at `src/ZdoRpgAi.Server.Console/ServerConfigJsonContext.cs`. When changing `ServerConfig` or its nested types, update `example/server-config.example.yaml` to match.

# RPC / Message Communication

Messages flow between three parties: **Server**, **Client**, and **Mod** (OpenMW Lua). The transport is WebSocket (server↔client) and file-based IPC (client↔mod), but the code uses the same `RpcChannel`/`IRpcChannel` abstraction for both.

## Key files when adding or handling messages

1. **Message payload definitions** — define the record types and enums:
   - `src/ZdoRpgAi.Protocol/Messages/Server.cs` — Server → Client and Server → Mod messages
   - `src/ZdoRpgAi.Protocol/Messages/Client.cs` — Client → Server, Client → Mod, Client → Both messages
   - `src/ZdoRpgAi.Protocol/Messages/Mod.cs` — Mod → Server messages
2. **JSON source generation** — every new payload type must be registered here for AOT compatibility:
   - `src/ZdoRpgAi.Protocol/Messages/PayloadJsonContext.cs` — add `[JsonSerializable(typeof(YourPayload))]`
3. **Server-side handling** — where incoming messages are routed and processed:
   - `src/ZdoRpgAi.Server/Game/GameRunner.cs` — `OnMessageReceived` switch, add handler methods here
4. **Client-side handling** — where the client processes messages from server and mod:
   - `src/ZdoRpgAi.Client/App/ClientApplication.cs` — `HandleServerMessage` and `HandleModMessage`

## RPC patterns

- **One-way (fire-and-forget):** `rpc.Publish(type, payload)` — no response expected.
- **Request-response:** `rpc.CallAsync(type, payload)` — awaits a response. The other side uses `rpc.Respond(type, responseTo, payload)`.
- **Serialization:** Use `JsonExtensions.SerializeToObject(payload, PayloadJsonContext.Default.PayloadType)` to create payloads. Use `msg.Json?.DeserializeSafe(PayloadJsonContext.Default.PayloadType)` to read them — this logs an error on failure instead of silently returning null.
- **Message routing:** Match on `msg.Type` using `nameof(MessageTypeEnum.Value)`.
