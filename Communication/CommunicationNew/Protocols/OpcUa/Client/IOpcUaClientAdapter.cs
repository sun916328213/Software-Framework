namespace CommunicationNew.Protocols.OpcUa;

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
