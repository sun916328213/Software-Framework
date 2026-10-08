// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ExecutionGraphScopeTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// ExecutionGraphScopeTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class ExecutionGraphScopeTests
{
    /// <summary>
    /// 测试相关入口 Create_ExcludedNodeAndItsConnections_AreNotExecutable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Create、Flow、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Create_ExcludedNodeAndItsConnections_AreNotExecutable()
    {
        var start = New<ValueNode>("Start").Vm;
        var parked = New<ValueNode>("Parked").Vm;
        var active = New<ValueNode>("Active").Vm;
        parked.IsExcludedFromExecution = true;

        var graph = ExecutionGraphScope.Create(
            new List<NodeModel> { start.Model, parked.Model, active.Model },
            new List<ConnectionModel> { Flow(start, parked), Flow(start, active), Flow(parked, active) });

        Assert.Equal([start.Model, active.Model], graph.Nodes);
        var connection = Assert.Single(graph.Connections);
        Assert.Equal(start.Id, connection.SourceNodeId);
        Assert.Equal(active.Id, connection.TargetNodeId);
    }

    /// <summary>
    /// 测试相关入口 Create_SeparatesTopLevelAndContainerLaneScopes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Create、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Create_SeparatesTopLevelAndContainerLaneScopes()
    {
        var top = New<ValueNode>("Top").Vm;
        var laneOne = New<ValueNode>("LaneOne").Vm;
        var laneTwo = New<ValueNode>("LaneTwo").Vm;
        laneOne.ParentExecutionContainerId = "container";
        laneOne.ExecutionLaneId = "lane-1";
        laneTwo.ParentExecutionContainerId = "container";
        laneTwo.ExecutionLaneId = "lane-2";

        var nodes = new[] { top.Model, laneOne.Model, laneTwo.Model };
        var topScope = ExecutionGraphScope.Create(nodes, []);
        var firstLane = ExecutionGraphScope.Create(nodes, [], "container", "lane-1");

        Assert.Equal([top.Model], topScope.Nodes);
        Assert.Equal([laneOne.Model], firstLane.Nodes);
    }
}
