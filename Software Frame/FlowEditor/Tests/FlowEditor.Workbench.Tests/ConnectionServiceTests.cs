// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ConnectionServiceTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Services、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Services;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// ConnectionServiceTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class ConnectionServiceTests
{
    private readonly ConnectionService _service = new();

    /// <summary>
    /// 验证 PlanConnection_RejectsIncompatibleDataTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateOutput、CreateInput、PlanConnection、Empty、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PlanConnection_RejectsIncompatibleDataTypes()
    {
        var source = PortModel.CreateOutput("value", PortType.Integer, "source");
        var target = PortModel.CreateInput("value", PortType.String, "target");
        var connections = new List<ConnectionModel>();

        var result = _service.PlanConnection(source, target, Array.Empty<NodeModel>(), connections);

        Assert.False(result.Success);
        Assert.Contains("类型不兼容", result.Error);
        Assert.Empty(connections);
    }

    /// <summary>
    /// 验证 PlanConnection_ReturnsPlanWithoutMutatingExistingConnections 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateOutput、CreateInput、PlanConnection、Empty、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PlanConnection_ReturnsPlanWithoutMutatingExistingConnections()
    {
        var source = PortModel.CreateOutput("value", PortType.Integer, "source");
        var target = PortModel.CreateInput("value", PortType.Integer, "target");
        var connections = new List<ConnectionModel>();

        var result = _service.PlanConnection(source, target, Array.Empty<NodeModel>(), connections);

        Assert.True(result.Success);
        Assert.Empty(connections);
        Assert.Same(result.PrimaryConnection, Assert.Single(result.Connections));
    }

    /// <summary>
    /// 验证 PlanConnection_FlowConnectionIncludesMatchingTypedBindings 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、First、PlanConnection、True、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PlanConnection_FlowConnectionIncludesMatchingTypedBindings()
    {
        var (source, sourceModel) = New<ValueNode>("source");
        var (target, targetModel) = New<ValueNode>("target");
        var sourceFlow = source.OutputPorts.First(p => p.DataType == PortType.Flow);
        var targetFlow = target.InputPorts.First(p => p.DataType == PortType.Flow);
        var connections = new List<ConnectionModel>();

        var result = _service.PlanConnection(
            sourceFlow,
            targetFlow,
            new NodeModel[] { source.Model, target.Model },
            connections);

        Assert.True(result.Success);
        Assert.Equal(1, result.AutoBoundCount);
        Assert.Empty(connections);
        Assert.Equal(2, result.Connections.Count);
        Assert.Equal(PortType.Integer, sourceModel.OutputPorts.First(p => p.Name == "Value").DataType);
        Assert.Equal(PortType.Integer, targetModel.InputPorts.First(p => p.Name == "Value").DataType);
    }

    /// <summary>
    /// 验证 PlanConnection_RejectsSecondTargetFromSameFlowOutput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、PlanConnection、Single、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PlanConnection_RejectsSecondTargetFromSameFlowOutput()
    {
        var (source, _) = New<CountingNode>("source");
        var (first, _) = New<CountingNode>("first");
        var (second, _) = New<CountingNode>("second");
        var existing = new List<ConnectionModel> { Flow(source, first) };

        var result = _service.PlanConnection(
            source.OutputPorts.Single(),
            second.InputPorts.Single(),
            [source.Model, first.Model, second.Model],
            existing);

        Assert.False(result.Success);
        Assert.Contains("并行分叉", result.Error);
    }

    /// <summary>
    /// 验证 PlanConnection_RejectsSecondSourceIntoSameFlowInput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、PlanConnection、Single、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PlanConnection_RejectsSecondSourceIntoSameFlowInput()
    {
        var (first, _) = New<CountingNode>("first");
        var (second, _) = New<CountingNode>("second");
        var (target, _) = New<CountingNode>("target");
        var existing = new List<ConnectionModel> { Flow(first, target) };

        var result = _service.PlanConnection(
            second.OutputPorts.Single(),
            target.InputPorts.Single(),
            [first.Model, second.Model, target.Model],
            existing);

        Assert.False(result.Success);
        Assert.Contains("分支汇聚", result.Error);
    }

    /// <summary>
    /// 验证 PlanConnection_DataOutputMayFanOutToDifferentInputs 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Single、PlanConnection、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PlanConnection_DataOutputMayFanOutToDifferentInputs()
    {
        var (source, _) = New<SourceNode>("source");
        var (first, _) = New<ValueNode>("first");
        var (second, _) = New<ValueNode>("second");
        var output = source.OutputPorts.Single(port => port.Name == "Value");
        var firstInput = first.InputPorts.Single(port => port.Name == "Value");
        var secondInput = second.InputPorts.Single(port => port.Name == "Value");
        var existing = new List<ConnectionModel>
        {
            new()
            {
                SourceNodeId = source.Id,
                SourcePortId = output.Id,
                TargetNodeId = first.Id,
                TargetPortId = firstInput.Id
            }
        };

        var result = _service.PlanConnection(
            output, secondInput, [source.Model, first.Model, second.Model], existing);

        Assert.True(result.Success, result.Error);
    }

    /// <summary>
    /// 验证 PortTypeClassifier_MapsClrTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、FromClrType；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(typeof(bool), PortType.Boolean)]
    [InlineData(typeof(int), PortType.Integer)]
    [InlineData(typeof(double), PortType.Double)]
    [InlineData(typeof(string), PortType.String)]
    public void PortTypeClassifier_MapsClrTypes(Type clrType, PortType expected)
        => Assert.Equal(expected, PortTypeClassifier.FromClrType(clrType));
}
