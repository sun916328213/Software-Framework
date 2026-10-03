// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowLogRouteTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证结构化日志路由身份及运行序号隔离。</summary>
[Collection(FlowLogContextCollection.Name)]
public sealed class FlowLogRouteTests
{
    /// <summary>可读键相同时，结构化身份与运行序号仍保持独立。</summary>
    [Fact]
    public void AmbiguousDisplayKeys_KeepIdentityAndRunCountersSeparate()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var firstRoute = new FlowLogRoute($"{prefix}\\B", "C");
        var secondRoute = new FlowLogRoute(prefix, "B\\C");

        Assert.Equal(firstRoute.DisplayKey, secondRoute.DisplayKey);
        Assert.NotEqual(firstRoute, secondRoute);
        Assert.NotEqual(firstRoute.StableId, secondRoute.StableId);

        using (FlowLogContext.EnterProcess(firstRoute.ProcessName))
        using (FlowLogContext.EnterFlow(firstRoute.FlowName))
            Assert.Equal(1, FlowLogContext.RunId);

        using (FlowLogContext.EnterProcess(secondRoute.ProcessName))
        using (FlowLogContext.EnterFlow(secondRoute.FlowName))
            Assert.Equal(1, FlowLogContext.RunId);
    }

    /// <summary>空容器名称统一为 null，防止同一逻辑路由产生两个计数桶。</summary>
    [Fact]
    public void EmptyProcessName_NormalizesToSameRouteAsNull()
    {
        var emptyProcess = new FlowLogRoute(string.Empty, "FlowA");
        var nullProcess = new FlowLogRoute(null, "FlowA");

        Assert.Equal(nullProcess, emptyProcess);
        Assert.Equal("FlowA", emptyProcess.DisplayKey);
    }

    /// <summary>关闭标签页释放路由后，同名任务再次打开应从新的 #1 开始。</summary>
    [Fact]
    public void ReleaseRoute_ReopenedFlowStartsFromOne()
    {
        var processName = Guid.NewGuid().ToString("N");
        const string flowName = "ReopenedFlow";

        using (FlowLogContext.EnterProcess(processName))
        {
            using (FlowLogContext.EnterFlow(flowName))
                Assert.Equal(1, FlowLogContext.RunId);
            using (FlowLogContext.EnterFlow(flowName))
                Assert.Equal(2, FlowLogContext.RunId);
        }

        Assert.True(FlowLogContext.ReleaseRoute(processName, flowName));

        using (FlowLogContext.EnterProcess(processName))
        using (FlowLogContext.EnterFlow(flowName))
            Assert.Equal(1, FlowLogContext.RunId);
    }

    /// <summary>释放路由不破坏仍在收尾的执行作用域，也不污染随后重开的同名任务。</summary>
    [Fact]
    public void ReleaseRoute_ActiveScopeKeepsDetachedCounter()
    {
        var processName = Guid.NewGuid().ToString("N");
        const string flowName = "ActiveFlow";

        using (FlowLogContext.EnterProcess(processName))
        using (FlowLogContext.EnterFlow(flowName))
        {
            Assert.Equal(1, FlowLogContext.RunId);
            Assert.True(FlowLogContext.ReleaseRoute(processName, flowName));

            FlowLogContext.BumpRun();
            Assert.Equal(2, FlowLogContext.RunId);

            using (FlowLogContext.EnterFlow(flowName))
                Assert.Equal(1, FlowLogContext.RunId);

            Assert.Equal(2, FlowLogContext.RunId);
        }
    }

    /// <summary>标签重命名通过 ViewModel 生命周期入口释放旧路由，而不是遗留静态计数桶。</summary>
    [Fact]
    public void RenameTab_ReleasesOldRouteCounter()
    {
        var processName = Guid.NewGuid().ToString("N");
        const string oldTitle = "BeforeRename";
        var viewModel = TestFlowEditorFactory.CreateViewModel();
        viewModel.SetProcessName(processName);
        viewModel.Model.TabTitle = oldTitle;

        using (FlowLogContext.EnterProcess(processName))
        using (FlowLogContext.EnterFlow(oldTitle))
            Assert.Equal(1, FlowLogContext.RunId);

        viewModel.RenameTab("AfterRename");

        Assert.Equal("AfterRename", viewModel.Model.TabTitle);
        using (FlowLogContext.EnterProcess(processName))
        using (FlowLogContext.EnterFlow(oldTitle))
            Assert.Equal(1, FlowLogContext.RunId);
    }

    /// <summary>执行中的标题保持稳定，保证当前日志作用域与用户看到的任务身份一致。</summary>
    [Fact]
    public void RenameTab_WhileRunningIsRejected()
    {
        var viewModel = TestFlowEditorFactory.CreateViewModel();
        viewModel.Model.TabTitle = "RunningFlow";
        viewModel.Model.IsRunning = true;

        viewModel.RenameTab("UnexpectedRename");

        Assert.Equal("RunningFlow", viewModel.Model.TabTitle);
        Assert.Equal("流程执行期间不能重命名", viewModel.Model.StatusText);
    }

    /// <summary>兼容外部直接设置模型标题时，ViewModel 仍会释放旧日志路由。</summary>
    [Fact]
    public void DirectModelTitleChange_ReleasesOldRouteCounter()
    {
        var processName = Guid.NewGuid().ToString("N");
        const string oldTitle = "DirectBefore";
        var viewModel = TestFlowEditorFactory.CreateViewModel();
        viewModel.SetProcessName(processName);
        viewModel.Model.TabTitle = oldTitle;

        using (FlowLogContext.EnterProcess(processName))
        using (FlowLogContext.EnterFlow(oldTitle))
            Assert.Equal(1, FlowLogContext.RunId);

        viewModel.Model.TabTitle = "DirectAfter";

        using (FlowLogContext.EnterProcess(processName))
        using (FlowLogContext.EnterFlow(oldTitle))
            Assert.Equal(1, FlowLogContext.RunId);
    }

    /// <summary>外部直接写模型也不能在运行期间改变任务身份。</summary>
    [Fact]
    public void DirectModelTitleChange_WhileRunningIsRejected()
    {
        var viewModel = TestFlowEditorFactory.CreateViewModel();
        viewModel.Model.TabTitle = "StableTitle";
        viewModel.Model.IsRunning = true;

        viewModel.Model.TabTitle = "BypassedTitle";

        Assert.Equal("StableTitle", viewModel.Model.TabTitle);
    }
}
