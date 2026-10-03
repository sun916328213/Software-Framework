// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraTriggerSelectors。成员说明可结合下方具体实现阅读。
namespace Device.Core.Cameras;

/// <summary>
/// 常用 GenICam 触发选择器的稳定配置值。
/// </summary>
public static class CameraTriggerSelectors
{
    /// <summary>开始采集单帧图像。</summary>
    public const string FrameStart = "FrameStart";

    /// <summary>开始采集一组帧；部分线扫型号使用该选择器控制一次完整扫描。</summary>
    public const string FrameBurstStart = "FrameBurstStart";

    /// <summary>开始采集一行图像。</summary>
    public const string LineStart = "LineStart";
}
