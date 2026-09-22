using Device.Core.Cameras;

namespace Device.Drivers.Cameras;

/// <summary>
/// 把公共相机配置转换为各厂商适配器共同使用的连接期决策。
/// </summary>
internal static class CameraDriverConfiguration
{
    /// <summary>GenICam 帧开始触发器名称。</summary>
    internal const string FrameStartSelector = CameraTriggerSelectors.FrameStart;

    /// <summary>GenICam 行开始触发器名称。</summary>
    internal const string LineStartSelector = CameraTriggerSelectors.LineStart;

    /// <summary>
    /// 按设备配置顺序返回需要下发的触发器。线扫先设置行触发，再设置帧触发，
    /// 从而让最后选中的触发器保持为 FrameStart，兼容软件触发命令。
    /// </summary>
    internal static IReadOnlyList<CameraTriggerBinding> GetTriggerBindings(CameraOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.ScanType == CameraScanType.LineScan)
        {
            return
            [
                new CameraTriggerBinding(
                    options.LineTrigger.Selector ?? LineStartSelector,
                    options.LineTrigger),
                new CameraTriggerBinding(
                    options.FrameTrigger.Selector ?? FrameStartSelector,
                    options.FrameTrigger),
            ];
        }

        // 面阵相机保留相机当前的 TriggerSelector，只覆盖模式和触发源，
        // 兼容没有 FrameStart 节点或使用旧版 SFNC 的型号。
        return [new CameraTriggerBinding(options.FrameTrigger.Selector, options.FrameTrigger)];
    }

    /// <summary>判断 CaptureAsync 是否需要主动发送软件触发命令。</summary>
    internal static bool ShouldExecuteSoftwareTrigger(CameraOptions options) =>
        options.FrameTrigger.Mode == CameraTriggerMode.On
        && string.Equals(
            options.FrameTrigger.Source,
            CameraTriggerSources.Software,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>把公共触发模式转换为 GenICam 使用的符号。</summary>
    internal static string ToGenICamMode(CameraTriggerMode mode) =>
        mode == CameraTriggerMode.On ? "On" : "Off";
}

/// <summary>一条需要下发给相机的 GenICam 触发器配置。</summary>
/// <param name="Selector">触发选择器；为空时保留设备当前选择器。</param>
/// <param name="Settings">触发模式与来源。</param>
internal sealed record CameraTriggerBinding(string? Selector, CameraTriggerSettings Settings);
