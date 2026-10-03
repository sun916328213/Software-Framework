// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PortValueTransferTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证所有执行路径共享的端口值传递规则。</summary>
public sealed class PortValueTransferTests
{
    /// <summary>兼容端口完成赋值，并把实际传递值返回给编排调用方。</summary>
    [Fact]
    public void TryTransfer_CompatiblePorts_AssignsAndReturnsValue()
    {
        var source = new SourceNode { Name = "源节点" };
        var target = new ValueNode { Name = "目标节点" };
        var sourcePort = source.OutputPorts.Single(port => port.Name == "Value");
        var targetPort = target.InputPorts.Single(port => port.Name == "Value");
        sourcePort.Value = 42;

        var transferred = PortValueTransfer.TryTransfer(
            source, sourcePort.Id, target, targetPort.Id, out var value);

        Assert.True(transferred);
        Assert.Equal(42, value);
        Assert.Equal(42, targetPort.Value);
    }

    /// <summary>缺失端口静默跳过，并保留目标端口已有值。</summary>
    [Fact]
    public void TryTransfer_MissingPort_ReturnsFalseWithoutMutation()
    {
        var source = new SourceNode { Name = "源节点" };
        var target = new ValueNode { Name = "目标节点" };
        var targetPort = target.InputPorts.Single(port => port.Name == "Value");
        targetPort.Value = 7;

        var transferred = PortValueTransfer.TryTransfer(
            source, "missing-port", target, targetPort.Id, out var value);

        Assert.False(transferred);
        Assert.Null(value);
        Assert.Equal(7, targetPort.Value);
    }

    /// <summary>声明类型不兼容时产生稳定诊断，并在抛出前保留目标值。</summary>
    [Fact]
    public void TryTransfer_DeclaredTypeMismatch_ThrowsWithoutMutation()
    {
        var source = new SourceNode { Name = "源节点" };
        var target = new ValueNode { Name = "目标节点" };
        var sourcePort = source.OutputPorts.Single(port => port.Name == "Value");
        var targetPort = target.InputPorts.Single(port => port.Name == "Value");
        sourcePort.DataType = PortType.String;
        targetPort.DataType = PortType.Integer;
        targetPort.Value = 7;

        var exception = Assert.Throws<FlowChartTypeMismatchException>(() =>
            PortValueTransfer.TryTransfer(
                source, sourcePort.Id, target, targetPort.Id, out _));

        Assert.Contains("声明类型不兼容", exception.Message, StringComparison.Ordinal);
        Assert.Equal(7, targetPort.Value);
    }

    /// <summary>声明类型兼容但运行时值错误时产生稳定诊断，并在抛出前保留目标值。</summary>
    [Fact]
    public void TryTransfer_RuntimeValueMismatch_ThrowsWithoutMutation()
    {
        var source = new SourceNode { Name = "源节点" };
        var target = new ValueNode { Name = "目标节点" };
        var sourcePort = source.OutputPorts.Single(port => port.Name == "Value");
        var targetPort = target.InputPorts.Single(port => port.Name == "Value");
        sourcePort.DataType = PortType.Integer;
        sourcePort.Value = "不是整数";
        targetPort.DataType = PortType.Integer;
        targetPort.Value = 7;

        var exception = Assert.Throws<FlowChartTypeMismatchException>(() =>
            PortValueTransfer.TryTransfer(
                source, sourcePort.Id, target, targetPort.Id, out _));

        Assert.Contains("运行时类型: String", exception.Message, StringComparison.Ordinal);
        Assert.Equal(7, targetPort.Value);
    }
}
