namespace FlowEditor.Models;

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
