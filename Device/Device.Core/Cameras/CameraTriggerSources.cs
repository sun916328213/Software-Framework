namespace Device.Core.Cameras;

/// <summary>
/// 常用相机触发源的稳定配置值。
/// </summary>
/// <remarks>
/// 各厂商或型号可以支持本类未列出的 GenICam 触发源；此列表仅用于避免常用值由调用方手写。
/// </remarks>
public static class CameraTriggerSources
{
    /// <summary>由应用程序发送软件触发命令。</summary>
    public const string Software = "Software";

    /// <summary>相机的 Line0 外部输入。</summary>
    public const string Line0 = "Line0";

    /// <summary>相机的 Line1 外部输入。</summary>
    public const string Line1 = "Line1";

    /// <summary>相机的 Line2 外部输入。</summary>
    public const string Line2 = "Line2";

    /// <summary>相机的 Line3 外部输入。</summary>
    public const string Line3 = "Line3";

    /// <summary>相机内部的第一路旋转编码器触发源。</summary>
    public const string RotaryEncoder1 = "RotaryEncoder1";
}
