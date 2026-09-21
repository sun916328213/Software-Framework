namespace Device.Core.Devices;

/// <summary>
/// 新架构中所有设备共享的最小生命周期接口。
/// </summary>
public interface IDevice : IAsyncDisposable
{
    /// <summary>配置中唯一的设备名称。</summary>
    string Name { get; }

    /// <summary>当前连接状态。</summary>
    DeviceState State { get; }

    /// <summary>连接设备。</summary>
    /// <param name="cancellationToken">用于在开始连接前或驱动支持时取消连接。</param>
    /// <returns>包含连接是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开设备并释放连接资源；实现必须允许在连接失败或已经断开的状态下安全调用。
    /// </summary>
    /// <param name="cancellationToken">用于在开始断开前或驱动支持时取消操作。</param>
    /// <returns>包含断开是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default);
}
