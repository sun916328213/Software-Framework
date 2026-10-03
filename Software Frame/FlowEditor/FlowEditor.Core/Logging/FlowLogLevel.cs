// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowLogLevel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel。导入不代表所有类型都会在运行时加载。
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
