using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Channel;

namespace ZdoRpgAi.Client.Channel;

/// <summary>
/// IChannel implementation for communicating with the OpenMW Lua mod.
/// Sends messages by writing to a VFS-accessible file (mod reads via openmw.vfs).
/// Receives messages by tailing the OpenMW log for [ZDORPG_MSG] lines.
/// Receives acks by parsing [ZDORPG_ACK] lines from the log.
/// </summary>
public class OpenmwModChannel : IChannel {
    private static readonly ILog Log = Logger.Get<OpenmwModChannel>();

    private const string MsgPrefix = "[ZDORPG_MSG]";
    private const string AckPrefix = "[ZDORPG_ACK]";

    private readonly string _outFilePath;
    private readonly string _logFilePath;
    private readonly int _pollIntervalMs;
    private readonly CancellationTokenSource _cts = new();

    // Outgoing state
    private readonly List<(int Id, string Json)> _pendingMessages = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _lastProcessedModMsgId;

    // Incoming state (log tailing)
    private long _logPosition;
    private int _lastSeenModMsgId;

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    private const string OutFileName = "zdorpgai_to_mod.txt";

    public OpenmwModChannel(string dataDir, string logFilePath, int pollIntervalMs = 50) {
        _outFilePath = Path.Combine(dataDir, OutFileName);
        _logFilePath = logFilePath;
        _pollIntervalMs = pollIntervalMs;
    }

    public void SendMessage(Message msg) {
        if (msg.Binary != null) {
            Log.Warn("Binary messages are not supported by OpenmwModChannel, ignoring binary payload for {Type}", msg.Type);
        }

        _writeLock.Wait();
        try {
            var json = msg.ToJson().ToJsonString();
            Log.Trace("SEND {Type}: {Json}", msg.Type, json);
            _pendingMessages.Add((msg.Id, json));
            FlushOutFile();
        }
        finally {
            _writeLock.Release();
        }
    }

    public async Task RunAsync() {
        Log.Info("Tailing log: {Path}, writing to: {OutPath}", _logFilePath, _outFilePath);

        // Seek to end of log file so we only process new lines
        if (File.Exists(_logFilePath)) {
            _logPosition = new FileInfo(_logFilePath).Length;
        }

        try {
            while (!_cts.Token.IsCancellationRequested) {
                try {
                    ReadLogLines();
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    Log.Warn("Error reading log: {Error}", ex.Message);
                }
                await Task.Delay(_pollIntervalMs, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally {
            Disconnected?.Invoke();
        }
    }

    public void Close() {
        _cts.Cancel();
    }

    private void FlushOutFile() {
        var lines = new List<string>(_pendingMessages.Count + 1) {
            $"lastProcessedMessageId:{_lastProcessedModMsgId}"
        };
        foreach (var (_, json) in _pendingMessages) {
            lines.Add(json);
        }
        var content = string.Join('\n', lines) + '\n';
        var tmpPath = _outFilePath + ".tmp";
        File.WriteAllText(tmpPath, content);
        File.Move(tmpPath, _outFilePath, overwrite: true);
    }

    private void ReadLogLines() {
        if (!File.Exists(_logFilePath)) return;

        var fileInfo = new FileInfo(_logFilePath);

        // Handle log rotation (file got smaller)
        if (fileInfo.Length < _logPosition) {
            _logPosition = 0;
        }

        if (fileInfo.Length == _logPosition) return;

        string newContent;
        try {
            using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_logPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            newContent = reader.ReadToEnd();
            _logPosition = fs.Position;
        }
        catch (IOException) {
            return;
        }

        if (string.IsNullOrEmpty(newContent)) return;

        var ackedSomething = false;
        foreach (var line in newContent.Split('\n')) {
            var msgIdx = line.IndexOf(MsgPrefix, StringComparison.Ordinal);
            if (msgIdx >= 0) {
                var jsonStr = line[(msgIdx + MsgPrefix.Length)..];
                ProcessModMessage(jsonStr);
                continue;
            }

            var ackIdx = line.IndexOf(AckPrefix, StringComparison.Ordinal);
            if (ackIdx >= 0) {
                var idStr = line[(ackIdx + AckPrefix.Length)..].Trim();
                if (int.TryParse(idStr, out var ackedId)) {
                    ProcessModAck(ackedId);
                    ackedSomething = true;
                }
            }
        }

        if (ackedSomething) {
            _writeLock.Wait();
            try {
                FlushOutFile();
            }
            finally {
                _writeLock.Release();
            }
        }
    }

    private void ProcessModMessage(string jsonStr) {
        JsonObject? obj;
        try {
            obj = JsonNode.Parse(jsonStr)?.AsObject();
        }
        catch {
            Log.Warn("Failed to parse mod message JSON: {Json}", jsonStr);
            return;
        }
        if (obj == null) return;

        var msgId = obj["id"]?.GetValue<int>() ?? 0;
        if (msgId > 0 && msgId <= _lastSeenModMsgId) return;
        if (msgId > _lastSeenModMsgId) {
            _lastSeenModMsgId = msgId;
            _lastProcessedModMsgId = msgId;
        }

        var msg = Message.FromJson(obj);
        Log.Trace("RECV {Type}: {Json}", msg.Type, jsonStr);
        MessageReceived?.Invoke(msg);
    }

    private void ProcessModAck(int ackedId) {
        _writeLock.Wait();
        try {
            var before = _pendingMessages.Count;
            _pendingMessages.RemoveAll(m => m.Id <= ackedId);
            if (_pendingMessages.Count != before) {
                Log.Trace("Mod acked up to {Id}, removed {Count} messages", ackedId, before - _pendingMessages.Count);
            }
        }
        finally {
            _writeLock.Release();
        }
    }
}
