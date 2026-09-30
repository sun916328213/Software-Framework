using Secs;
using Secs4Net;

namespace SecsGem.Runtime;

public partial class SecsGemRuntime : ISecsGemLogger
{
    private bool _disposed;

    public bool IsConnected => _transport.IsConnected;

    public ushort DeviceID
    {
        get => _transport.DeviceID;
        set => _transport.DeviceID = value;
    }

    public int ID
    {
        get => _transport.ID;
        set => _transport.ID = value;
    }

    public event EventHandler<ConnectionState> OnConnectChanged
    {
        add => _transport.OnConnectChanged += value;
        remove => _transport.OnConnectChanged -= value;
    }

    public event SecsHandle OnSend
    {
        add => _transport.OnSend += value;
        remove => _transport.OnSend -= value;
    }

    public event SecsHandle OnReceive
    {
        add => _transport.OnReceive += value;
        remove => _transport.OnReceive -= value;
    }

    public event SecsHandle OnReply
    {
        add => _transport.OnReply += value;
        remove => _transport.OnReply -= value;
    }

    public event SecsLevelHandle OnInfo
    {
        add => _transport.OnInfo += value;
        remove => _transport.OnInfo -= value;
    }

    public event SecsLevelHandle OnWaring
    {
        add => _transport.OnWaring += value;
        remove => _transport.OnWaring -= value;
    }

    public event SecsLevelHandle OnError
    {
        add => _transport.OnError += value;
        remove => _transport.OnError -= value;
    }

    public event SecsLevelHandle OnDebug
    {
        add => _transport.OnDebug += value;
        remove => _transport.OnDebug -= value;
    }

    public virtual Task<bool> ConnectAsync(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535) =>
        _transport.ConnectAsync(isActiveMode, address, port, deviceId, bufferSize);

    public virtual bool Connect(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535) =>
        _transport.Connect(isActiveMode, address, port, deviceId, bufferSize);

    public virtual bool Connect() => _transport.Connect();

    public virtual Task CloseAsync() => _transport.CloseAsync();

    public virtual void Close() => _transport.Close();

    public virtual Task<SecsMessage> Send(string order, Item items, bool wait = false) =>
        _transport.Send(order, items, wait);

    public virtual Task<SecsMessage> Send(SecsMessage message, bool wait = false) =>
        _transport.Send(message, wait);

    public virtual Task<SecsMessage> Send(string sml, bool wait = false) =>
        _transport.Send(sml, wait);

    public virtual Task ReplyAsync(SecsMessage message, Item items) =>
        _transport.ReplyAsync(message, items);

    protected void RaiseError(string message) => _transport.Error(message);

    protected void RaiseWarning(string message) => _transport.Warning(message);

    public void MessageIn(SecsMessage message, int systemByte) =>
        _transport.MessageIn(message, systemByte);

    public void MessageOut(SecsMessage message, int systemByte) =>
        _transport.MessageOut(message, systemByte);

    public void Info(string message) => _transport.Info(message);

    public void Warning(string message) => _transport.Warning(message);

    public void Error(string message, SecsMessage secsMessage, Exception exception) =>
        _transport.Error(message, secsMessage, exception);

    public void Error(string message) => _transport.Error(message);

    public void Error(string message, Exception exception) =>
        _transport.Error(message, exception);

    public void Debug(string message) => _transport.Debug(message);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (!disposing) return;

        _transport.OnReceive -= SecsBase_OnReceiveAsync;
        _transport.OnReply -= SecsBase_OnReply;
        _transport.OnConnectChanged -= SecsControl_OnConnectChanged;
        _transport.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
