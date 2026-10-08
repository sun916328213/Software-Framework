// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InMemorySecsTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、SecsGem.Tests、SecsGem.Core、SecsGem.Runtime、Secs4Net、SecsGem.Runtime.Transport。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;
using SecsGem.Tests;
using SecsGem.Core;
using SecsGem.Runtime;
using Secs4Net;
using SecsGem.Runtime.Transport;

namespace SecsGem.Tests.Fakes;

/// <summary>
/// InMemorySecsTransport 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：ISecsTransport。具体约束按这些契约及各成员实现解释。
/// </summary>
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

    /// <summary>
    /// 测试相关入口 ConnectAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
    /// </summary>
    public Task<bool> ConnectAsync(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535)
    {
        DeviceID = checked((ushort)deviceId);
        IsConnected = true;
        OnConnectChanged?.Invoke(this, ConnectionState.Selected);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 测试相关入口 Connect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetResult、GetAwaiter、ConnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    public bool Connect(bool isActiveMode, string address, int port, int deviceId = 0, int bufferSize = 65535) =>
        ConnectAsync(isActiveMode, address, port, deviceId, bufferSize).GetAwaiter().GetResult();

    /// <summary>
    /// 测试相关入口 Connect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Connect；具体条件和先后次序见方法体。
    /// </summary>
    public bool Connect() => Connect(true, "127.0.0.1", 5000);

    /// <summary>
    /// 测试相关入口 CloseAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试相关入口 Close；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void Close()
    {
        IsConnected = false;
        OnConnectChanged?.Invoke(this, ConnectionState.Retry);
    }

    /// <summary>
    /// 测试相关入口 Send；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
    /// </summary>
    public Task<SecsMessage> Send(string order, Item items, bool wait = false)
    {
        LastSentOrder = order;
        LastSentItem = items;
        return Task.FromResult(new SecsMessage(1, 2));
    }

    /// <summary>
    /// 测试相关入口 Send；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Send；具体条件和先后次序见方法体。
    /// </summary>
    public Task<SecsMessage> Send(SecsMessage message, bool wait = false) =>
        Send($"S{message.S}F{message.F}", message.SecsItem!, wait);

    /// <summary>
    /// 测试相关入口 Send；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public Task<SecsMessage> Send(string sml, bool wait = false) =>
        throw new NotSupportedException("SML parsing is outside this transport fake.");

    /// <summary>
    /// 测试相关入口 ReplyAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public Task ReplyAsync(SecsMessage message, Item items)
    {
        LastReplyItem = items;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试相关入口 EmitReceivedAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public Task EmitReceivedAsync(SecsMessage message) =>
        OnReceive?.Invoke(message, string.Empty) ?? Task.CompletedTask;

    /// <summary>
    /// 测试相关入口 MessageIn；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void MessageIn(SecsMessage message, int systemByte) { }
    /// <summary>
    /// 测试相关入口 MessageOut；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void MessageOut(SecsMessage message, int systemByte) { }
    /// <summary>
    /// 测试相关入口 Info；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void Info(string message) => OnInfo?.Invoke(message);
    /// <summary>
    /// 测试相关入口 Warning；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void Warning(string message) => OnWaring?.Invoke(message);
    /// <summary>
    /// 测试相关入口 Error；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void Error(string message) => OnError?.Invoke(message);
    /// <summary>
    /// 测试相关入口 Error；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Error；具体条件和先后次序见方法体。
    /// </summary>
    public void Error(string message, SecsMessage secsMessage, Exception exception) => Error(message);
    /// <summary>
    /// 测试相关入口 Error；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Error；具体条件和先后次序见方法体。
    /// </summary>
    public void Error(string message, Exception exception) => Error(message);
    /// <summary>
    /// 测试相关入口 Debug；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public void Debug(string message) => OnDebug?.Invoke(message);
    /// <summary>
    /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose() => Close();
}
