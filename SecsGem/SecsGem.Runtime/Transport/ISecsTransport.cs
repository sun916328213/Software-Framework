using Secs;
using Secs4Net;

namespace SecsGem.Runtime.Transport;

/// <summary>Runtime 内部的 HSMS 收发边界；协议编排不直接持有 secs4net 连接。</summary>
internal interface ISecsTransport : IDisposable
{
    bool IsConnected { get; }
    ushort DeviceID { get; set; }
    int ID { get; set; }

    event EventHandler<ConnectionState> OnConnectChanged;
    event SecsHandle OnSend;
    event SecsHandle OnReceive;
    event SecsHandle OnReply;
    event SecsLevelHandle OnInfo;
    event SecsLevelHandle OnWaring;
    event SecsLevelHandle OnError;
    event SecsLevelHandle OnDebug;

    Task<bool> ConnectAsync(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535);
    bool Connect(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535);
    bool Connect();
    Task CloseAsync();
    void Close();

    Task<SecsMessage> Send(string order, Item items, bool wait = false);
    Task<SecsMessage> Send(SecsMessage message, bool wait = false);
    Task<SecsMessage> Send(string sml, bool wait = false);
    Task ReplyAsync(SecsMessage message, Item items);

    void MessageIn(SecsMessage message, int systemByte);
    void MessageOut(SecsMessage message, int systemByte);
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Error(string message, SecsMessage secsMessage, Exception exception);
    void Error(string message, Exception exception);
    void Debug(string message);
}
