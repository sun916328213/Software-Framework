// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：AINode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization、FlowEditor.Controls.Nodes、FlowEditor.Core、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using System.Text.Json.Serialization;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using Vision.Core.Imaging;
using Vision.Controls.ViewModels;
using Vision.Controls.View;
using System.Windows;
using Vision.Tools.AI.Detection;
using Vision.Core.AI;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Vision.SingleTool.AI;

/// <summary>
/// AINode 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
/// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
/// </summary>
[FlowTool("AI检测", "RobotOutline", ToolGroup = "视觉", ToolSubGroup = "AI", SortIndex = 40, Description = "调用 AI 模型执行推理检测")]
internal class AINode : NodeModel
{
    public DetectionToolSession Tool { get; set; } = new();
    [NodeProperty(DisplayName = "输入图像", IsInput = true), JsonIgnore]
    public VisionImage? Image { get; set; }
    [NodeProperty(DisplayName = "推理后端", IsInput = true), JsonIgnore]
    public IAIProvider? Provider { get => Tool.Provider; set => Tool.Provider = value; }
    [NodeProperty(DisplayName = "检测结果", IsOutput = true), JsonIgnore]
    public AIDetectionResult? Result => Tool.Results.OfType<AIDetectionResult>().LastOrDefault();
    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、Run；具体条件和先后次序见方法体。
    /// </summary>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Tool.Image = Image;
        Tool.Run();
        if (Tool.ToolState == global::Vision.Core.Tools.E_ToolState.Failed) throw new InvalidOperationException(Tool.RunMessage);
        return Task.CompletedTask;
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EditTool。
    /// 正文中的关键协作调用包括 ShowDialog；具体条件和先后次序见方法体。
    /// </summary>
    [NodeAction("编辑", Order = 0, IconResource = "CodeBraces")]
    public void EditTool() => new Window { Title = "AI 检测", Content = new ToolEditorView { Tool = Tool }, Width = 900, Height = 700 }.ShowDialog();
}
