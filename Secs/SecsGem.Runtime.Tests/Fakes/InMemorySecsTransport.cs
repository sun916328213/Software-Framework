using Secs;
using Secs4Net;
using SecsGem.Runtime.Transport;

namespace SecsGem.Runtime.Tests.Fakes;

internal sealed class InMemorySecsTransport : ISecsTransport
{
    public bool IsConnected { get; private set; }
    public ushort DeviceID { get; set; }
    public int ID { get; set; }
    public string? LastSentOrder { get; private set; }
    public Item? LastSentItem { get; private set; }
    public Item? LastReplyItem { get; private set; }

    public event EventHandler<ConnectionState>? OnConnectChanged;
    public event SecsHandle? OnSend;
    public event SecsHandle? OnReceive;
    public event SecsHandle? OnReply;
    public event SecsLevelHandle? OnInfo;
    public event SecsLevelHandle? OnWaring;
    public event SecsLevelHandle? OnError;
    public event SecsLevelHandle? OnDebug;

    public Task<bool> ConnectAsync(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535)
    {
        DeviceID = checked((ushort)deviceId);
        IsConnected = true;
        OnConnectChanged?.Invoke(this, ConnectionState.Selected);
        return Task.FromResult(true);
    }

    public bool Connect(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535) =>
        ConnectAsync(isActiveMode, address, port, deviceId, bufferSize).GetAwaiter().GetResult();

    public bool Connect() => Connect(true, "127.0.0.1", 5000);

    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    public void Close()
    {
        IsConnected = false;
        OnConnectChanged?.Invoke(this, ConnectionState.Retry);
    }

    public Task<SecsMessage> Send(string order, Item items, bool wait = false)
    {
        LastSentOrder = order;
        LastSentItem = items;
        return Task.FromResult(new SecsMessage(1, 2));
    }

    public Task<SecsMessage> Send(SecsMessage message, bool wait = false) =>
        Send($"S{message.S}F{message.F}", message.SecsItem!, wait);

    public Task<SecsMessage> Send(string sml, bool wait = false) =>
        throw new NotSupportedException("SML parsing is outside this transport fake.");

    public Task ReplyAsync(SecsMessage message, Item items)
    {
        LastReplyItem = items;
        return Task.CompletedTask;
    }

    public Task EmitReceivedAsync(SecsMessage message) =>
        OnReceive?.Invoke(message, string.Empty) ?? Task.CompletedTask;

    public void MessageIn(SecsMessage message, int systemByte) { }
    public void MessageOut(SecsMessage message, int systemByte) { }
    public void Info(string message) => OnInfo?.Invoke(message);
    public void Warning(string message) => OnWaring?.Invoke(message);
    public void Error(string message) => OnError?.Invoke(message);
    public void Error(string message, SecsMessage secsMessage, Exception exception) => Error(message);
    public void Error(string message, Exception exception) => Error(message);
    public void Debug(string message) => OnDebug?.Invoke(message);
    public void Dispose() => Close();
}
