namespace CommunicationNew.Abstractions;

/// <summary>连接生命周期的最小能力接口。</summary>
public interface IConnection : IAsyncDisposable
{
    /// <summary>当前生命周期状态。</summary>
    ConnectionState State { get; }

    /// <summary>建立连接；重复调用已连接实例不会创建第二条连接。</summary>
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>取消活动操作并等待连接关闭。</summary>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}
