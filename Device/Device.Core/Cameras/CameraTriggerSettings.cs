namespace Device.Core.Cameras;

/// <summary>
/// 一路相机触发器的模式和触发源配置。
/// </summary>
public sealed record CameraTriggerSettings
{
    /// <summary>
    /// 可选的 GenICam 触发选择器。为空时面阵相机保留设备当前选择器，
    /// 线扫相机分别使用 FrameStart 和 LineStart 默认值。
    /// </summary>
    public string? Selector { get; init; }

    /// <summary>触发器的启用状态。</summary>
    public CameraTriggerMode Mode { get; init; } = CameraTriggerMode.On;

    /// <summary>
    /// 触发源。启用触发时不能为空；除常用值外，也可以填写具体型号支持的 GenICam 符号。
    /// </summary>
    public string Source { get; init; } = CameraTriggerSources.Software;
}
