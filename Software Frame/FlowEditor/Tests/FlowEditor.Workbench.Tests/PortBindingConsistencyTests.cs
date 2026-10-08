// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PortBindingConsistencyTests、PropertyNode、CommunicationBaseNode、TcpConnectNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Services;
using WPFCustomControls.PropertyGrid.Core.Models;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// PortBindingConsistencyTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class PortBindingConsistencyTests
{
    /// <summary>
    /// PropertyNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private class PropertyNode : NodeModel
    {
        [NodeProperty(DisplayName = "属性A")]
        public string A { get; set; } = string.Empty;

        [NodeProperty(DisplayName = "属性B")]
        public string B { get; set; } = string.Empty;

        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// CommunicationBaseNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private class CommunicationBaseNode : NodeModel
    {
        [NodeProperty(DisplayName = "设备名称")]
        public string DeviceName { get; set; } = "TCP1";

        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// TcpConnectNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：CommunicationBaseNode。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TcpConnectNode : CommunicationBaseNode;

    /// <summary>
    /// 测试相关入口 DisablePropertyOutput_RemovesItsConnection_AndReplacementPortCanReconnect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、SetPortConfig、nameof、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DisablePropertyOutput_RemovesItsConnection_AndReplacementPortCanReconnect()
    {
        var service = TestFlowEditorFactory.CreateService();
        var sourceModel = new PropertyNode { Name = "工具1" };
        var targetModel = new PropertyNode { Name = "工具2" };
        var source = service.Canvas.AddNode(sourceModel);
        var target = service.Canvas.AddNode(targetModel);
        sourceModel.SetPortConfig(nameof(PropertyNode.A), false, true);
        targetModel.SetPortConfig(nameof(PropertyNode.A), true, false);
        var sourceA = source.OutputPorts.Single(port => port.PropertyName == nameof(PropertyNode.A));
        var targetA = target.InputPorts.Single(port => port.PropertyName == nameof(PropertyNode.A));
        service.Connections.Add(new ConnectionModel
        {
            SourceNodeId = source.Id,
            SourcePortId = sourceA.Id,
            TargetNodeId = target.Id,
            TargetPortId = targetA.Id
        });

        sourceModel.SetPortConfig(nameof(PropertyNode.A), false, false);

        Assert.Empty(service.Connections);
        sourceModel.SetPortConfig(nameof(PropertyNode.B), false, true);
        var sourceB = source.OutputPorts.Single(port => port.PropertyName == nameof(PropertyNode.B));
        var plan = new ConnectionService().PlanConnection(
            sourceB, targetA, service.Nodes.Select(node => node.Model).ToList(), service.Connections);
        Assert.True(plan.Success, plan.Error);
    }

    /// <summary>
    /// 测试相关入口 BindingUpstreamNodes_ContainsOnlyTransitivePredecessors；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、GetUpstreamFlowNodes；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BindingUpstreamNodes_ContainsOnlyTransitivePredecessors()
    {
        var service = TestFlowEditorFactory.CreateService();
        var before = service.Canvas.AddNode(new CountingNode { Name = "前置工具" });
        var current = service.Canvas.AddNode(new PropertyNode { Name = "当前工具" });
        var after = service.Canvas.AddNode(new CountingNode { Name = "后续工具" });
        var unrelated = service.Canvas.AddNode(new CountingNode { Name = "无关工具" });
        service.Connections.Add(Flow(before, current));
        service.Connections.Add(Flow(current, after));

        var upstream = ((IDataBindingProvider)current.Model).GetUpstreamFlowNodes();

        var item = Assert.Single(upstream);
        Assert.Equal(before.Id, item.NodeId);
        Assert.DoesNotContain(upstream, node => node.NodeId == after.Id || node.NodeId == unrelated.Id);
    }

    /// <summary>
    /// 测试相关入口 BindableOutputProperties_IncludePropertiesDeclaredOnToolBaseClasses；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetBindableProperties、Contains、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BindableOutputProperties_IncludePropertiesDeclaredOnToolBaseClasses()
    {
        var node = new TcpConnectNode();

        var properties = node.GetBindableProperties();

        Assert.Contains(nameof(CommunicationBaseNode.DeviceName), properties);
    }

    /// <summary>
    /// 测试相关入口 RemovePropertyPort_ClearsInlineCheckStateAndRaisesConfigChanged；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 SetPortConfig、nameof、Single、True、RemoveCustomPort；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RemovePropertyPort_ClearsInlineCheckStateAndRaisesConfigChanged()
    {
        var node = new PropertyNode();
        node.SetPortConfig(nameof(PropertyNode.A), isInput: true, isOutput: false);
        var port = node.InputPorts.Single(item => item.PropertyName == nameof(PropertyNode.A));
        var changeCount = 0;
        node.ConfigChanged += () => changeCount++;

        Assert.True(node.RemoveCustomPort(port.Id));

        Assert.Equal((false, false), node.GetPortConfig(nameof(PropertyNode.A)));
        Assert.Equal(1, changeCount);
    }
}
