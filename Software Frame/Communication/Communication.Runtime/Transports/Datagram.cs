// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：Datagram。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net。导入不代表所有类型都会在运行时加载。
using System.Net;

namespace Communication.Runtime.Transports;

/// <summary>保留单个 UDP 消息边界以及远端端点的数据报。</summary>
public readonly record struct Datagram
{
    /// <summary>创建一个待发送或已接收的数据报。</summary>
    /// <param name="payload">完整数据报负载。</param>
    /// <param name="remoteEndpoint">发送目标或接收来源。</param>
    public Datagram(ReadOnlyMemory<byte> payload, IPEndPoint remoteEndpoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        Payload = payload;
        RemoteEndpoint = remoteEndpoint;
    }

    /// <summary>完整数据报负载。</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>发送目标或接收来源。</summary>
    public IPEndPoint RemoteEndpoint { get; }
}
