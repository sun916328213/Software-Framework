// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IOpcUaClientAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.OpcUa。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.OpcUa;
namespace Communication.Runtime.Protocols.OpcUa;

/// <summary>OPC UA 客户端 SDK 的内部 seam，隐藏证书、端点发现和 Session 生命周期。</summary>
internal interface IOpcUaClientAdapter : IAsyncDisposable
{
    /// <summary>底层 OPC UA Session 是否已连接。</summary>
    bool IsConnected { get; }

    /// <summary>发现端点并建立 OPC UA Session。</summary>
    ValueTask ConnectAsync(CancellationToken cancellationToken);

    /// <summary>关闭 Session 和底层通道。</summary>
    ValueTask DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>读取 ByteString 或 Byte 类型节点。</summary>
    ValueTask<byte[]> ReadAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>把字节值写入指定节点。</summary>
    ValueTask WriteAsync(string nodeId, byte[] value, CancellationToken cancellationToken);
}
