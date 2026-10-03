// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：FlowNodesEditorAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes、FlowNodes.Script、FlowNodes.Vision.Contracts、ScriptEditor.Controls.Views、System.Windows。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowNodes.Script;
using FlowNodes.Vision.Contracts;
using ScriptEditor.Controls.Views;
using System.Windows;
using Vision.Controls.View;
using Vision.Core.Tools;

namespace FlowNodes.Presentation.NodeEditing;

/// <summary>FlowNodes 的 WPF 编辑 adapter；节点自身不创建窗口或编辑器控件。</summary>
public sealed class FlowNodesEditorAdapter : INodeEditorHandler, INodeEditorLauncher
{
    internal static FlowNodesEditorAdapter Default { get; } = new();

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryOpen。
    /// 正文中的关键协作调用包括 Open；具体条件和先后次序见方法体。
    /// </summary>
    public bool TryOpen(NodeModel node)
    {
        if (node is not ScriptNode && node is not IVisionNodeEditorSource) return false;
        Open(node);
        return true;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Open。
    /// 正文中的关键协作调用包括 CreateWindow、ShowDialog、ApplyEditorState；具体条件和先后次序见方法体。
    /// </summary>
    public void Open(NodeModel node)
    {
        var window = CreateWindow(node);
        window.ShowDialog();
        if (node is ScriptNode script && window.Content is ScriptEditorView editor)
            script.ApplyEditorState(editor.Document);
    }

    /// <summary>构造原编辑窗口，显示与状态回写由 Open 完成。</summary>
    public Window CreateWindow(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var (title, content) = node switch
        {
            ScriptNode script => ($"脚本编辑器 - {node.Name}", (object)new ScriptEditorView(
                $"脚本编辑器 - {node.Name}", script.EditorState, script.BuildInputs(), script.LastOutput)),
            IVisionNodeEditorSource vision => ($"视觉工具 - {node.Name}", (object)new ToolEditorView
                { Tool = vision.PrepareToolForEditing() }),
            _ => throw new NotSupportedException($"节点类型 [{node.GetType().Name}] 没有注册专用编辑器。")
        };
        return new Window
        {
            Title = title,
            Content = content,
            Owner = Application.Current?.MainWindow,
            Width = 900,
            Height = 700,
            SizeToContent = SizeToContent.Manual,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Style = Application.Current?.TryFindResource("MaterialDesignWindow") as Style,
        };
    }
}
