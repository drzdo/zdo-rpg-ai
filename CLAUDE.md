# C# Conventions

- **Events**: Use past tense verb without `On` prefix (e.g. `Disconnected`, `MessageReceived`, `KeyPressed`). The `On` prefix is reserved for the method that raises the event.
- **Async methods**: Suffix with `Async` for methods returning `Task`/`Task<T>` (e.g. `RunAsync`).
- **Logging**: Use `ZdoRpgAi.Core.ILog` via `Logger.Get<T>()`. Method names: `Trace`, `Debug`, `Info`, `Warn`, `Error`. Do not reference Serilog directly outside of Core.
- **No XML doc comments** (`<summary>`) unless they add value beyond what the name already communicates.
- **Native AOT**: Do not use features or libraries that are incompatible with native AOT compilation (e.g. unconstrained reflection, `dynamic`, non-trimming-safe APIs).
- **JSON config**: Client config uses `System.Text.Json` source generation for AOT compatibility. The `JsonSerializerContext` is at `src/ZdoRpgAi.Client/Bootstrap/ClientConfigJsonContext.cs`. Only the root type (`ClientConfig`) needs `[JsonSerializable]` — nested types are discovered automatically.
