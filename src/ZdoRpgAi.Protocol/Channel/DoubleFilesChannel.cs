using System.Text.Json.Nodes;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Protocol.Channel;

public class DoubleFilesChannel : IChannel {
    private static readonly ILog Log = Logger.Get<DoubleFilesChannel>();

    private readonly string _outFilePath;
    private readonly string _inFilePath;
    private readonly int _pollIntervalMs;
    private readonly CancellationTokenSource _cts = new();

    // Outgoing state
    private readonly List<(int Id, string Json)> _pendingMessages = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _lastProcessedInId;

    // Incoming state
    private int _lastSeenInId;

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    public DoubleFilesChannel(string outFilePath, string inFilePath, int pollIntervalMs = 50) {
        _outFilePath = outFilePath;
        _inFilePath = inFilePath;
        _pollIntervalMs = pollIntervalMs;
    }

    public void SendMessage(Message msg) {
        if (msg.Binary != null) {
            Log.Warn("Binary messages are not supported by DoubleFilesChannel, ignoring binary payload for {Type}", msg.Type);
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
        Log.Info("Polling file: {Path}", _inFilePath);
        try {
            while (!_cts.Token.IsCancellationRequested) {
                try {
                    ReadInFile();
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    Log.Warn("Error reading file: {Error}", ex.Message);
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
            $"lastProcessedMessageId:{_lastProcessedInId}"
        };
        foreach (var (_, json) in _pendingMessages) {
            lines.Add(json);
        }
        var content = string.Join('\n', lines) + '\n';
        var tmpPath = _outFilePath + ".tmp";
        File.WriteAllText(tmpPath, content);
        File.Move(tmpPath, _outFilePath, overwrite: true);
    }

    private void ReadInFile() {
        if (!File.Exists(_inFilePath)) return;

        string[] lines;
        try {
            using var fs = new FileStream(_inFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (IOException) {
            return;
        }

        if (lines.Length == 0) return;

        // First line: lastProcessedMessageId:$ID — ack from the other side
        var header = lines[0].Trim();
        const string prefix = "lastProcessedMessageId:";
        if (header.StartsWith(prefix) && int.TryParse(header[prefix.Length..], out var ackedId)) {
            _writeLock.Wait();
            try {
                var before = _pendingMessages.Count;
                _pendingMessages.RemoveAll(m => m.Id <= ackedId);
                if (_pendingMessages.Count != before) {
                    FlushOutFile();
                }
            }
            finally {
                _writeLock.Release();
            }
        }

        // Remaining lines: JSON messages
        for (var i = 1; i < lines.Length; i++) {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            JsonObject? obj;
            try {
                obj = JsonNode.Parse(line)?.AsObject();
            }
            catch {
                continue;
            }
            if (obj == null) continue;

            var msgId = obj["id"]?.GetValue<int>() ?? 0;
            if (msgId > 0 && msgId <= _lastSeenInId) continue;
            if (msgId > _lastSeenInId) {
                _lastSeenInId = msgId;
                _lastProcessedInId = msgId;
            }

            var msg = Message.FromJson(obj);
            Log.Trace("RECV {Type}: {Json}", msg.Type, line);
            MessageReceived?.Invoke(msg);
        }

        // Flush ack back if we processed new messages
        if (_lastProcessedInId > 0) {
            _writeLock.Wait();
            try {
                FlushOutFile();
            }
            finally {
                _writeLock.Release();
            }
        }
    }
}
