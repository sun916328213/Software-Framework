// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowToolAttribute、NodeActionAttribute、NodeActionDescriptor。成员说明可结合下方具体实现阅读。
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

    /// <summary>创建指定显示名称的节点动作标记。</summary>
    /// <param name="buttonName">动作按钮显示名称。</param>
    public NodeActionAttribute(string buttonName)
    {
        ButtonName = buttonName;
    }
}

/// <summary>节点动作的纯元数据描述，不包含 WPF 命令或 UI 回调。</summary>
/// <param name="Id">节点类型内稳定的动作标识。</param>
/// <param name="Name">用户可见名称。</param>
/// <param name="IconResource">图标资源键。</param>
/// <param name="Order">显示排序权重。</param>
public sealed record NodeActionDescriptor(
    string Id,
    string Name,
    string IconResource,
    int Order);
