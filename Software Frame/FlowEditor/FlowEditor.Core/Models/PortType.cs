// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PortType。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core.Models;

/// <summary>流程图端口支持的数据类型。</summary>
public enum PortType
{
    /// <summary>任意类型（通配）。</summary>
    Any,
    /// <summary>布尔值。</summary>
    Boolean,
    /// <summary>整数（int/long）。</summary>
    Integer,
    /// <summary>浮点数（double/float）。</summary>
    Double,
    /// <summary>字符串。</summary>
    String,
    /// <summary>图像（HObject/Bitmap 等）。</summary>
    Image,
    /// <summary>通用 object。</summary>
    Object,

    /// <summary>流程控制（执行流入口/出口，用于 Start→Node→...→End 的连线驱动）。</summary>
    Flow
}
