#nullable enable

namespace Vision.Core.Diagnostics;

/// <summary>
/// 可用于界面/日志的稳定诊断，不携带厂商异常或SDK对象。
/// </summary>
public sealed record VisionIssue
{
    /// <summary>
    /// 用于程序判断和日志定位的稳定诊断代码。
    /// </summary>
    public string Code
    {
        get;
    }

    /// <summary>
    /// 面向调用者的中文诊断说明。
    /// </summary>
    public string Message
    {
        get;
    }

    /// <summary>
    /// 创建包含稳定代码和说明的诊断信息。
    /// </summary>
    public VisionIssue(
        string code,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }
}
