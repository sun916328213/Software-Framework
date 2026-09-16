using System.ComponentModel;

namespace FlowEditor.Core;

/// <summary>流程运行日志的宿主无关等级。</summary>
public enum FlowLogLevel
{
    /// <summary>普通运行信息。</summary>
    [Description("信息")]
    Information,

    /// <summary>需要关注但不终止流程的警告。</summary>
    [Description("警告")]
    Warning,

    /// <summary>警告状态已复位。</summary>
    [Description("警告复位")]
    Reset,

    /// <summary>节点或流程执行错误。</summary>
    [Description("错误")]
    Error,

    /// <summary>宿主或系统级错误。</summary>
    [Description("系统错误")]
    SysError
}
