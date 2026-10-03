// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowRecursionGuardTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using Xunit;

namespace FlowEditor.Workbench.Tests;

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
