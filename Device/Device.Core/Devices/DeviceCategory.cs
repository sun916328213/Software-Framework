namespace Device.Core.Devices;

/// <summary>
/// 可扩展的设备类别。
/// </summary>
/// <remarks>
/// 该类型刻意不使用枚举，后续增加扫码器、RFID 等类别时无需修改公共程序集。
/// </remarks>
public readonly record struct DeviceCategory
{
    /// <summary>
    /// 相机类别。
    /// </summary>
    public static DeviceCategory Camera { get; } = new("camera");

    /// <summary>
    /// 光源控制器类别。
    /// </summary>
    public static DeviceCategory LightController { get; } = new("light-controller");

    /// <summary>
    /// 扫码器类别。
    /// </summary>
    public static DeviceCategory Scanner { get; } = new("scanner");

    /// <summary>
    /// RFID 读写器类别。
    /// </summary>
    public static DeviceCategory Rfid { get; } = new("rfid");

    /// <summary>
    /// 打印机类别。
    /// </summary>
    public static DeviceCategory Printer { get; } = new("printer");

    /// <summary>工业相机采集卡。</summary>
    public static DeviceCategory FrameGrabber { get; } = new("frame-grabber");

    /// <summary>标签模板打印软件。</summary>
    public static DeviceCategory LabelPrintEngine { get; } = new("label-print-engine");

    /// <summary>
    /// 创建可扩展的设备类别。
    /// </summary>
    /// <param name="value">不可为空的类别标识。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空或仅包含空白字符。</exception>
    public DeviceCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Device category cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// 设备类别的稳定标识值。
    /// </summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value;
}
