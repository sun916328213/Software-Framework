using FlowEditor.Nodes;

namespace FlowEditor.Core;

/// <summary>
/// 全局扫描特性，标记在继承 NodeModel 的节点类上，用于工具箱自动注册。
/// 程序启动时反射扫描所有标记此特性的节点工具。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class FlowToolAttribute : Attribute
{
    /// <summary>工具在工具箱中的显示名称。</summary>
    public string ToolName { get; }

    /// <summary>图标资源路径（MaterialDesign PackIcon 枚举名称）。</summary>
    public string IconResource { get; }

    /// <summary>排序权重（越小越靠前），默认 0。</summary>
    public int SortIndex { get; set; } = 0;

    /// <summary>工具分组名称（如 "流程控制"、"通讯"、"视觉" 等）。</summary>
    public string ToolGroup { get; set; } = "常规";

    /// <summary>工具子分组名称（如 "PLC"、"串口"、"相机" 等），为空表示直接挂在一级分组下。</summary>
    public string ToolSubGroup { get; set; } = string.Empty;

    /// <summary>鼠标悬浮提示描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否显示在新建节点工具箱；关闭后仍可反序列化历史流程。</summary>
    public bool VisibleInToolbox { get; set; } = true;

    /// <summary>
    /// 标记节点工具。
    /// </summary>
    /// <param name="toolName">显示名称</param>
    /// <param name="iconResource">图标名称（PackIconKind 枚举值字符串形式）</param>
    public FlowToolAttribute(string toolName, string iconResource)
    {
        ToolName = toolName;
        IconResource = iconResource;
        if(Description==string.Empty)
            Description = toolName;
    }
}

/// <summary>
/// 节点功能按钮特性，标记节点方法为底部操作按钮。
/// 程序自动扫描渲染 UI 按钮，绑定独立点击事件。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class NodeActionAttribute : Attribute
{
    /// <summary>按钮显示名称。</summary>
    public string ButtonName { get; }

    /// <summary>按钮排序权重（越小越靠前）。</summary>
    public int Order { get; set; } = 0;

    /// <summary>按钮图标。</summary>
    public string IconResource { get; set; } = "DotsHorizontal";

    public NodeActionAttribute(string buttonName)
    {
        ButtonName = buttonName;
    }
}
