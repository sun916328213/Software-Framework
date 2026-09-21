namespace Device.Core.LabelPrinting;

/// <summary>
/// 标签打印引擎配置违反运行时约束。
/// </summary>
public sealed class LabelPrintEngineConfigurationException : Exception
{
    /// <summary>创建包含全部配置问题的异常。</summary>
    /// <param name="errors">全部配置错误。</param>
    public LabelPrintEngineConfigurationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    /// <summary>发现的全部配置错误。</summary>
    public IReadOnlyList<string> Errors { get; }
}
