namespace ZdoRpgAi.Protocol.Channel;

public interface IChannel {
    event Action<Message>? MessageReceived;
    event Action? Disconnected;

    void SendMessage(Message msg);
    Task RunAsync();
    void Close();
}
