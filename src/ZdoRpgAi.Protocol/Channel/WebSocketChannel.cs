using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Protocol.Channel;

public class WebSocketChannel : IChannel {
    private static readonly ILog Log = Logger.Get<WebSocketChannel>();

    private readonly WebSocket _socket;
    private readonly int _maxMessageSize;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event Action<Message>? MessageReceived;
    public event Action? Disconnected;

    public WebSocketChannel(WebSocket socket, int maxMessageSize) {
        _socket = socket;
        _maxMessageSize = maxMessageSize;
    }

    public void SendMessage(Message msg) {
        _ = SendAsync(msg);
    }

    private async Task SendAsync(Message msg) {
        try {
            var json = msg.ToJson();
            if (msg.Binary != null) {
                Log.Trace("SEND binary {Type}: {Bytes} bytes", msg.Type, msg.Binary.Length);
                var bytes = SerializeBinary(json, msg.Binary);
                await _sendLock.WaitAsync(_cts.Token);
                try {
                    await _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, _cts.Token);
                }
                finally {
                    _sendLock.Release();
                }
            }
            else {
                var jsonStr = json.ToJsonString();
                Log.Trace("SEND {Type}: {Json}", msg.Type, jsonStr);
                var bytes = Encoding.UTF8.GetBytes(jsonStr);
                await _sendLock.WaitAsync(_cts.Token);
                try {
                    await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
                }
                finally {
                    _sendLock.Release();
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) {
            Log.Warn("Send failed: {Error}", ex.Message);
        }
        catch (Exception ex) {
            Log.Error("Send error: {Error}", ex.ToString());
        }
    }

    public async Task RunAsync() {
        await Task.CompletedTask;
    }

    public async Task RunWebSocketAsync() {
        var buffer = new byte[_maxMessageSize];
        try {
            while (_socket.State == WebSocketState.Open) {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do {
                    result = await _socket.ReceiveAsync(buffer, _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) {
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text) {
                    var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                    var obj = JsonNode.Parse(json)?.AsObject();
                    if (obj == null) {
                        continue;
                    }

                    var msg = Message.FromJson(obj, null);
                    Log.Trace("RECV {Type}: {Json}", msg.Type, json);
                    MessageReceived?.Invoke(msg);
                }
                else if (result.MessageType == WebSocketMessageType.Binary) {
                    var (obj, binary) = DeserializeBinary(ms.GetBuffer(), (int)ms.Length);
                    var msg = Message.FromJson(obj, binary);
                    Log.Trace("RECV binary {Type}: {Bytes} bytes", msg.Type, binary.Length);
                    MessageReceived?.Invoke(msg);
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        finally {
            Disconnected?.Invoke();
        }
    }

    public void Close() {
        _cts.Cancel();
    }

    private static byte[] SerializeBinary(JsonObject msg, byte[] binary) {
        var jsonBytes = Encoding.UTF8.GetBytes(msg.ToJsonString());
        var buf = new byte[4 + jsonBytes.Length + 4 + binary.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), jsonBytes.Length);
        jsonBytes.CopyTo(buf, 4);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4 + jsonBytes.Length, 4), binary.Length);
        binary.CopyTo(buf, 4 + jsonBytes.Length + 4);
        return buf;
    }

    private static (JsonObject msg, byte[] binary) DeserializeBinary(byte[] data, int length) {
        var jsonSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4));
        var json = Encoding.UTF8.GetString(data, 4, jsonSize);
        var msg = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("Invalid JSON in binary message");
        var binOffset = 4 + jsonSize;
        var binSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(binOffset, 4));
        var binary = new byte[binSize];
        Array.Copy(data, binOffset + 4, binary, 0, binSize);
        return (msg, binary);
    }
}
