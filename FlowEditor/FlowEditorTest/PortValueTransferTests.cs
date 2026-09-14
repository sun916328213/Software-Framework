using FlowEditor.Core;
using FlowEditor.Models;
using Xunit;

namespace FlowEditorTest;

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
