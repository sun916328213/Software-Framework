// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using Prism.Ioc;
using Secs4Net;

namespace SecsGem.Runtime;

/// <summary>
/// SecsGemRuntime 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// 继承或实现的契约：ISecsGemLogger。具体约束按这些契约及各成员实现解释。
/// </summary>
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

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    public virtual Task<bool> ConnectAsync(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535) =>
        StartControlListener(
            _transport.ConnectAsync(isActiveMode, address, port, deviceId, bufferSize));

    /// <summary>
    /// 连接建立后启动 Host 主动控制消息的监听（S1F13/S1F17/S1F15 → 切状态 + S6F11 上报）。
    /// 不监听就不会有人在 Host 下发 S1F17 后切控制状态——收发分离的另一半。
    /// </summary>
    private async Task<bool> StartControlListener(Task<bool> connect)
    {
        if (!await connect.ConfigureAwait(false)) return false;
        if (_controlListenerCts == null)
        {
            _controlListenerCts = new CancellationTokenSource();
            var token = _controlListenerCts.Token;
            _controlListener = Task.Run(
                async () =>
                {
                    try
                    {
                        await _container.Resolve<ConnectManager>().WaitOnlineAndOfflineAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { /* 释放时取消，正常结束 */ }
                    catch (Exception ex) { RaiseError($"控制消息监听异常: {ex.Message}"); }
                },
                CancellationToken.None);
        }
        return true;
    }

    /// <summary>
    /// 发起连接操作；对应入口为 Connect。
    /// 正文中的关键协作调用包括 Connect；具体条件和先后次序见方法体。
    /// </summary>
    public virtual bool Connect(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535) =>
        _transport.Connect(isActiveMode, address, port, deviceId, bufferSize);

    /// <summary>
    /// 发起连接操作；对应入口为 Connect。
    /// 正文中的关键协作调用包括 Connect；具体条件和先后次序见方法体。
    /// </summary>
    public virtual bool Connect() => _transport.Connect();

    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseAsync。
    /// 正文中的关键协作调用包括 CloseAsync；具体条件和先后次序见方法体。
    /// </summary>
    public virtual Task CloseAsync() => _transport.CloseAsync();

    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    public virtual void Close() => _transport.Close();

    /// <summary>
    /// 执行发送操作；对应入口为 Send。
    /// 正文中的关键协作调用包括 Send；具体条件和先后次序见方法体。
    /// </summary>
    public virtual Task<SecsMessage> Send(string order, Item items, bool wait = false) =>
        _transport.Send(order, items, wait);

    /// <summary>
    /// 执行发送操作；对应入口为 Send。
    /// 正文中的关键协作调用包括 Send；具体条件和先后次序见方法体。
    /// </summary>
    public virtual Task<SecsMessage> Send(SecsMessage message, bool wait = false) =>
        _transport.Send(message, wait);

    /// <summary>
    /// 执行发送操作；对应入口为 Send。
    /// 正文中的关键协作调用包括 Send；具体条件和先后次序见方法体。
    /// </summary>
    public virtual Task<SecsMessage> Send(string sml, bool wait = false) =>
        _transport.Send(sml, wait);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReplyAsync。
    /// 正文中的关键协作调用包括 ReplyAsync；具体条件和先后次序见方法体。
    /// </summary>
    public virtual Task ReplyAsync(SecsMessage message, Item items) =>
        _transport.ReplyAsync(message, items);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RaiseError。
    /// 正文中的关键协作调用包括 Error；具体条件和先后次序见方法体。
    /// </summary>
    protected void RaiseError(string message) => _transport.Error(message);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RaiseWarning。
    /// 正文中的关键协作调用包括 Warning；具体条件和先后次序见方法体。
    /// </summary>
    protected void RaiseWarning(string message) => _transport.Warning(message);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MessageIn。
    /// 正文中的关键协作调用包括 MessageIn；具体条件和先后次序见方法体。
    /// </summary>
    public void MessageIn(SecsMessage message, int systemByte) =>
        _transport.MessageIn(message, systemByte);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MessageOut。
    /// 正文中的关键协作调用包括 MessageOut；具体条件和先后次序见方法体。
    /// </summary>
    public void MessageOut(SecsMessage message, int systemByte) =>
        _transport.MessageOut(message, systemByte);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Info。
    /// 正文中的关键协作调用包括 Info；具体条件和先后次序见方法体。
    /// </summary>
    public void Info(string message) => _transport.Info(message);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Warning。
    /// 正文中的关键协作调用包括 Warning；具体条件和先后次序见方法体。
    /// </summary>
    public void Warning(string message) => _transport.Warning(message);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// 正文中的关键协作调用包括 Error；具体条件和先后次序见方法体。
    /// </summary>
    public void Error(string message, SecsMessage secsMessage, Exception exception) =>
        _transport.Error(message, secsMessage, exception);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// 正文中的关键协作调用包括 Error；具体条件和先后次序见方法体。
    /// </summary>
    public void Error(string message) => _transport.Error(message);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// 正文中的关键协作调用包括 Error；具体条件和先后次序见方法体。
    /// </summary>
    public void Error(string message, Exception exception) =>
        _transport.Error(message, exception);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Debug。
    /// 正文中的关键协作调用包括 Debug；具体条件和先后次序见方法体。
    /// </summary>
    public void Debug(string message) => _transport.Debug(message);

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (!disposing) return;

        _controlListenerCts?.Cancel();
        _controlListenerCts?.Dispose();
        _transport.OnReceive -= SecsBase_OnReceiveAsync;
        ProcessProgramsDeleted = null;
        FormattedProcessProgramReceived = null;
        _transport.OnReply -= SecsBase_OnReply;
        _transport.OnConnectChanged -= SecsControl_OnConnectChanged;
        _transport.Dispose();
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose、SuppressFinalize；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
