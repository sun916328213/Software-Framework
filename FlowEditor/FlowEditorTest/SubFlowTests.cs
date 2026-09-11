using FlowEditor.Core;
using Xunit;

namespace FlowEditorTest;

/// <summary>验证流程递归深度防护的上限与作用域恢复。</summary>
public sealed class FlowRecursionGuardTests
{
    /// <summary>递归深度达到上限后拒绝继续进入。</summary>
    [Fact]
    public void Enter_BeyondMaximumDepth_Throws()
    {
        var scopes = new List<IDisposable>();
        try
        {
            for (var index = 0; index < FlowRecursionGuard.MaxDepth; index++)
                scopes.Add(FlowRecursionGuard.Enter());

            Assert.Throws<InvalidOperationException>(() => FlowRecursionGuard.Enter());
            Assert.Equal(FlowRecursionGuard.MaxDepth, FlowRecursionGuard.CurrentDepth);
        }
        finally
        {
            foreach (var scope in scopes)
                scope.Dispose();
        }
    }

    /// <summary>离开递归作用域后恢复进入前的深度。</summary>
    [Fact]
    public void Dispose_RestoresPreviousDepth()
    {
        using (FlowRecursionGuard.Enter())
        {
            Assert.Equal(1, FlowRecursionGuard.CurrentDepth);
        }

        Assert.Equal(0, FlowRecursionGuard.CurrentDepth);
    }
}
