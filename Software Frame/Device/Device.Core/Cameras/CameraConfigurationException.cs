// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraConfigurationException。成员说明可结合下方具体实现阅读。
namespace Device.Core.Cameras;

/// <summary>
/// 相机配置违反运行时约束。
/// </summary>
public sealed class CameraConfigurationException : Exception
{
    /// <summary>
    /// 创建相机配置异常。
    /// </summary>
    /// <param name="errors">全部配置错误。</param>
    public CameraConfigurationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    /// <summary>发现的全部配置错误。</summary>
    public IReadOnlyList<string> Errors { get; }
}
