namespace Device.Core.Devices;

/// <summary>
/// 可扩展的设备生命周期状态。
/// </summary>
public readonly record struct DeviceState
{
    /// <summary>设备尚未连接。</summary>
    public static DeviceState Disconnected { get; } = new("disconnected");
    /// <summary>设备正在建立连接。</summary>
    public static DeviceState Connecting { get; } = new("connecting");
    /// <summary>设备连接正常。</summary>
    public static DeviceState Connected { get; } = new("connected");
    /// <summary>设备发生故障，需要断开或重连。</summary>
    public static DeviceState Faulted { get; } = new("faulted");

    /// <summary>
    /// 创建设备状态。
    /// </summary>
    /// <param name="value">不可为空的状态标识。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空或仅包含空白字符。</exception>
    public DeviceState(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Device state cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>状态的稳定标识值。</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
