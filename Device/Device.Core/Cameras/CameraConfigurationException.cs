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
