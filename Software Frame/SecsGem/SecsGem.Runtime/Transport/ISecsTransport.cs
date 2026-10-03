// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ISecsTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
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

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// </summary>
    Task<bool> ConnectAsync(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535);
    /// <summary>
    /// 发起连接操作；对应入口为 Connect。
    /// </summary>
    bool Connect(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535);
    /// <summary>
    /// 发起连接操作；对应入口为 Connect。
    /// </summary>
    bool Connect();
    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseAsync。
    /// </summary>
    Task CloseAsync();
    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// </summary>
    void Close();

    /// <summary>
    /// 执行发送操作；对应入口为 Send。
    /// </summary>
    Task<SecsMessage> Send(string order, Item items, bool wait = false);
    /// <summary>
    /// 执行发送操作；对应入口为 Send。
    /// </summary>
    Task<SecsMessage> Send(SecsMessage message, bool wait = false);
    /// <summary>
    /// 执行发送操作；对应入口为 Send。
    /// </summary>
    Task<SecsMessage> Send(string sml, bool wait = false);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReplyAsync。
    /// </summary>
    Task ReplyAsync(SecsMessage message, Item items);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MessageIn。
    /// </summary>
    void MessageIn(SecsMessage message, int systemByte);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MessageOut。
    /// </summary>
    void MessageOut(SecsMessage message, int systemByte);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Info。
    /// </summary>
    void Info(string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Warning。
    /// </summary>
    void Warning(string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// </summary>
    void Error(string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// </summary>
    void Error(string message, SecsMessage secsMessage, Exception exception);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// </summary>
    void Error(string message, Exception exception);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Debug。
    /// </summary>
    void Debug(string message);
}
