// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraDriverConfiguration、CameraTriggerBinding。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Cameras。导入不代表所有类型都会在运行时加载。
using Device.Core.Cameras;

namespace Device.Runtime.Cameras;

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
        return GetTriggerBindings(options.ScanType, options.FrameTrigger, options.LineTrigger);
    }

    /// <summary>
    /// 按扫描类型和一组触发配置返回需要下发的触发器；运行时改触发与连接期下发共用此实现。
    /// </summary>
    /// <param name="scanType">扫描类型；面阵只用帧触发，线扫先下行触发再下帧触发。</param>
    /// <param name="frameTrigger">帧触发配置。</param>
    /// <param name="lineTrigger">行触发配置；面阵相机忽略。</param>
    internal static IReadOnlyList<CameraTriggerBinding> GetTriggerBindings(
        CameraScanType scanType,
        CameraTriggerSettings frameTrigger,
        CameraTriggerSettings lineTrigger)
    {
        ArgumentNullException.ThrowIfNull(frameTrigger);
        ArgumentNullException.ThrowIfNull(lineTrigger);

        if (scanType == CameraScanType.LineScan)
        {
            return
            [
                new CameraTriggerBinding(
                    lineTrigger.Selector ?? LineStartSelector,
                    lineTrigger),
                new CameraTriggerBinding(
                    frameTrigger.Selector ?? FrameStartSelector,
                    frameTrigger),
            ];
        }

        // 面阵相机保留相机当前的 TriggerSelector，只覆盖模式和触发源，
        // 兼容没有 FrameStart 节点或使用旧版 SFNC 的型号。
        return [new CameraTriggerBinding(frameTrigger.Selector, frameTrigger)];
    }

    /// <summary>判断 CaptureAsync 是否需要主动发送软件触发命令。</summary>
    internal static bool ShouldExecuteSoftwareTrigger(CameraOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ShouldExecuteSoftwareTrigger(options.FrameTrigger);
    }

    /// <summary>
    /// 判断按给定帧触发配置采集时是否需要主动发送软件触发命令。
    /// </summary>
    /// <remarks>
    /// 运行时改过触发后必须以「当前生效的帧触发」判断，不能用创建时的
    /// <see cref="CameraOptions.FrameTrigger"/>，否则切到外部触发后仍会多发一条软件触发命令。
    /// </remarks>
    internal static bool ShouldExecuteSoftwareTrigger(CameraTriggerSettings frameTrigger)
    {
        ArgumentNullException.ThrowIfNull(frameTrigger);
        return frameTrigger.Mode == CameraTriggerMode.On
            && string.Equals(
                frameTrigger.Source,
                CameraTriggerSources.Software,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 校验运行时下发的触发配置；返回 <c>null</c> 表示可以下发，否则返回失败原因。
    /// </summary>
    /// <param name="scanType">扫描类型；面阵相机的行触发不会被下发，因此不参与校验。</param>
    /// <param name="frameTrigger">帧触发配置。</param>
    /// <param name="lineTrigger">行触发配置。</param>
    internal static string? ValidateTriggerSettings(
        CameraScanType scanType,
        CameraTriggerSettings frameTrigger,
        CameraTriggerSettings lineTrigger)
    {
        ArgumentNullException.ThrowIfNull(frameTrigger);
        ArgumentNullException.ThrowIfNull(lineTrigger);

        var frameError = ValidateTrigger(frameTrigger, "帧");
        if (frameError is not null)
            return frameError;

        // 面阵相机只下发帧触发，行触发配置无效值不应阻断帧触发的下发。
        return scanType == CameraScanType.LineScan ? ValidateTrigger(lineTrigger, "行") : null;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateTrigger。
    /// 正文中的关键协作调用包括 IsDefined、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private static string? ValidateTrigger(CameraTriggerSettings trigger, string triggerName)
    {
        if (!Enum.IsDefined(trigger.Mode))
            return $"{triggerName}触发模式 '{trigger.Mode}' 无效。";

        if (trigger.Mode == CameraTriggerMode.On && string.IsNullOrWhiteSpace(trigger.Source))
            return $"启用{triggerName}触发时必须指定触发源。";

        return null;
    }

    /// <summary>把公共触发模式转换为 GenICam 使用的符号。</summary>
    internal static string ToGenICamMode(CameraTriggerMode mode) =>
        mode == CameraTriggerMode.On ? "On" : "Off";
}

/// <summary>一条需要下发给相机的 GenICam 触发器配置。</summary>
/// <param name="Selector">触发选择器；为空时保留设备当前选择器。</param>
/// <param name="Settings">触发模式与来源。</param>
internal sealed record CameraTriggerBinding(string? Selector, CameraTriggerSettings Settings);
