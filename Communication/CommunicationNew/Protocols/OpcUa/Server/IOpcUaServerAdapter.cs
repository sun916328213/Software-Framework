namespace CommunicationNew.Protocols.OpcUa;

/// <summary>OPC UA 服务端 SDK 的内部 seam，隐藏地址空间与 Server 生命周期。</summary>
internal interface IOpcUaServerAdapter : IAsyncDisposable
{
    /// <summary>底层 OPC UA Server 是否正在监听。</summary>
    bool IsRunning { get; }

    /// <summary>已注册节点的兼容 NodeId 集合。</summary>
    IReadOnlyCollection<string> Nodes { get; }

    /// <summary>在服务器启动前注册一个 ByteString 变量节点。</summary>
    void RegisterNode(string nodeId, byte[] initialValue);

    /// <summary>构建地址空间并启动真实 OPC UA Server。</summary>
    ValueTask StartAsync(CancellationToken cancellationToken);

    /// <summary>停止监听并等待 Server 释放拥有的任务和通道。</summary>
    ValueTask StopAsync(CancellationToken cancellationToken);

    /// <summary>读取节点的当前 ByteString 值。</summary>
    byte[] Read(string nodeId);

    /// <summary>更新节点的 ByteString 值并通知订阅者。</summary>
    void Write(string nodeId, byte[] value);
}
