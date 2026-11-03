using FlowEditor.Controls.Nodes.FlowControl;
using Xunit;

namespace FlowEditor.Workbench.Tests;

public sealed class SwitchRequiredInputTests
{
    [Fact]
    public async Task ExistingUnboundConstantBehavior_RemainsDefault()
    {
        var node = new SwitchNode { InputValue = true, RulesText = "true=OK\nfalse=NG" };
        Assert.False(node.RequireInputConnection);
        await node.ExecuteAsync(CancellationToken.None);
        Assert.Equal("OK", node.LastMatchedBranch);
    }

    [Fact]
    public async Task RequiredInput_DoesNotUseConstantOrPreviousInput_WhenDetachedOrUnwired()
    {
        var node = new SwitchNode { RequireInputConnection = true, InputValue = true, RulesText = "true=OK\nfalse=NG" };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Contains("输入连接", error.Message); Assert.Contains("缺少", node.LastMatchedBranch);
    }
}
