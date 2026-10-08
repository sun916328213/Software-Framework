// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NodeInteractionSeamTests、RecordingNodeEditorLauncher、RecordingNodeNotificationSink。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows.Input、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Nodes.FlowControl、FlowEditor.Controls.Services。导入不代表所有类型都会在运行时加载。
using System.Windows.Input;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证节点交互只通过宿主 seam 发出，不直接创建 WPF 对象。</summary>
public sealed class NodeInteractionSeamTests
{
    /// <summary>ToolBlock 和 Switch 均把编辑请求交给同一启动器。</summary>
    [Fact]
    public void EditorActions_AreForwardedToInjectedLauncher()
    {
        var launcher = new RecordingNodeEditorLauncher();
        var service = CreateService(nodeEditorLauncher: launcher);
        var toolBlock = new ToolBlockNode();
        var dispatch = new SwitchNode();
        service.Canvas.AddNode(toolBlock);
        service.Canvas.AddNode(dispatch);

        toolBlock.OpenInternalFlow();
        dispatch.EditDispatchRules();

        Assert.Equal([toolBlock, dispatch], launcher.Requests);
    }

    /// <summary>Loop 只发布参数摘要，具体展示方式由宿主决定。</summary>
    [Fact]
    public void LoopPreview_IsForwardedAsInformation()
    {
        var notifications = new RecordingNodeNotificationSink();
        var service = CreateService(nodeNotifications: notifications);
        var loop = new LoopNode { Iterations = 7, CurrentIndex = 3 };
        service.Canvas.AddNode(loop);

        loop.PreviewParams();

        var request = Assert.Single(notifications.Requests);
        Assert.Equal("循环参数预览", request.Title);
        Assert.Equal(loop.GetParameterSummary(), request.Message);
        Assert.Contains("循环次数: 7", request.Message);
        Assert.Contains("当前索引: 3", request.Message);
    }

    /// <summary>动作描述只包含元数据，不把表现层命令或委托泄漏给节点模型。</summary>
    [Fact]
    public void ActionDescriptor_DoesNotExposeCommandOrDelegate()
    {
        var descriptor = Assert.Single(new ToolBlockNode().ActionDescriptors);
        var propertyTypes = descriptor.GetType()
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToList();

        Assert.DoesNotContain(propertyTypes, type => typeof(ICommand).IsAssignableFrom(type));
        Assert.DoesNotContain(propertyTypes, type => typeof(Delegate).IsAssignableFrom(type));
    }

    /// <summary>表现层 adapter 按动作 ID 调用节点内部实现。</summary>
    [Fact]
    public void ActionViewModelCommand_ExecutesDeclaredNodeAction()
    {
        var loop = new LoopNode { CurrentIndex = 4 };
        var viewModel = new NodeViewModel(loop);
        var reset = Assert.Single(viewModel.ActionButtons, action => action.Name == "重置计数");

        reset.Command.Execute(null);

        Assert.Equal(0, loop.CurrentIndex);
    }

    /// <summary>创建只用于节点交互验证的流程图模块。</summary>
    private static FlowChartService CreateService(
        INodeEditorLauncher? nodeEditorLauncher = null,
        INodeNotificationSink? nodeNotifications = null) =>
        TestFlowEditorFactory.CreateService(
            nodeEditorLauncher: nodeEditorLauncher,
            nodeNotifications: nodeNotifications);

    /// <summary>记录节点编辑请求的测试 adapter。</summary>
    private sealed class RecordingNodeEditorLauncher : INodeEditorLauncher
    {
        /// <summary>已收到的节点顺序。</summary>
        public List<NodeModel> Requests { get; } = [];

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 Open；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </remarks>
        public void Open(NodeModel node) => Requests.Add(node);
    }

    /// <summary>记录信息提示的测试 adapter。</summary>
    private sealed class RecordingNodeNotificationSink : INodeNotificationSink
    {
        /// <summary>已收到的提示顺序。</summary>
        public List<(string Title, string Message)> Requests { get; } = [];

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowInformation；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </remarks>
        public void ShowInformation(string title, string message)
            => Requests.Add((title, message));
    }
}
