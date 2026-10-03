// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowChartServiceExecutionTests、RecordingFlowLogSink、ThrowingFlowLogSink、CancellableNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>从 FlowChartService 运行入口到执行引擎的集成回归。</summary>
[Collection(FlowLogContextCollection.Name)]
public class FlowChartServiceExecutionTests
{
    /// <summary>
    /// RecordingFlowLogSink 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IFlowLogSink。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingFlowLogSink : IFlowLogSink
    {
        /// <summary>线程安全记录收到的结构化日志。</summary>
        public ConcurrentQueue<FlowLogRecord> Records { get; } = new();

        /// <inheritdoc />
        /// <remarks>
        /// 验证 Write 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Enqueue；具体条件和先后次序见方法体。
        /// </remarks>
        public void Write(FlowLogRecord record) => Records.Enqueue(record);
    }

    /// <summary>
    /// ThrowingFlowLogSink 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IFlowLogSink。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ThrowingFlowLogSink : IFlowLogSink
    {
        /// <inheritdoc />
        /// <remarks>
        /// 验证 Write 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Contains；具体条件和先后次序见方法体。
        /// </remarks>
        public void Write(FlowLogRecord record)
        {
            if (record.Message.Contains("执行完成"))
                throw new System.IO.IOException("日志磁盘不可用");
        }
    }

    /// <summary>
    /// CancellableNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CancellableNode : NodeModel
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、Delay；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }

    /// <summary>
    /// 验证 RunAsync_ExecutesActiveGraphAndSkipsExcludedNodes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RunAsync_ExecutesActiveGraphAndSkipsExcludedNodes()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var activeModel = new CountingNode { Name = "Active" };
        var active = service.Canvas.AddNode(activeModel);
        var parkedModel = new CountingNode
        {
            Name = "Parked",
            IsExcludedFromExecution = true
        };
        service.Canvas.AddNode(parkedModel);
        service.Connections.Add(Flow(start, active));
        var logs = new ConcurrentQueue<string>();

        await service.Execution.RunAsync(logs.Enqueue);

        Assert.Equal(1, activeModel.Executions);
        Assert.Equal(0, parkedModel.Executions);
        Assert.Contains(logs, message => message.Contains("开始执行流程"));
        Assert.Contains(logs, message => message.Contains("执行完成"));
        Assert.Equal("执行完成", service.Model.StatusText);
        Assert.False(service.Model.IsRunning);
    }

    /// <summary>
    /// 验证 RunAsync_NodeFailure_AlwaysRestoresRuntimeState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RunAsync_NodeFailure_AlwaysRestoresRuntimeState()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var failing = service.Canvas.AddNode(new FailNode { Name = "Failing" });
        service.Connections.Add(Flow(start, failing));

        await service.Execution.RunAsync(_ => { });

        Assert.Equal(NodeStatus.Error, failing.Status);
        Assert.Equal("执行出错", service.Model.StatusText);
        Assert.False(service.Model.IsRunning);
        Assert.False(service.Model.IsDebugMode);
        Assert.False(service.Model.IsPaused);
    }

    /// <summary>验证 IoC 日志 sink 收到完整流程上下文和节点声明的日志等级。</summary>
    [Fact]
    public async Task RunAsync_InjectedLogSinkReceivesContextAndDeclaredLevel()
    {
        var sink = new RecordingFlowLogSink();
        var service = TestFlowEditorFactory.CreateService(flowLogSink: sink);
        service.ProcessName = "AutoFlow";
        var start = service.Canvas.AddNode(new StartNode());
        var logNode = service.Canvas.AddNode(new LogNode
        {
            Name = "WarnLog",
            Message = "需要关注",
            Level = FlowLogLevel.Warning
        });
        service.Connections.Add(Flow(start, logNode));

        await service.Execution.RunAsync(_ => { });

        Assert.True(sink.Records.Any(record =>
            record.ProcessName == "AutoFlow" &&
            record.FlowName == service.Model.TabTitle &&
            record.LogKey == $"AutoFlow\\{service.Model.TabTitle}" &&
            record.NodeName == "WarnLog" &&
            record.Level == FlowLogLevel.Warning &&
            record.Message.Contains("需要关注")),
            string.Join(Environment.NewLine, sink.Records.Select(record =>
                $"{record.Level}|{record.NodeName ?? "<null>"}|{record.Message}")));
        Assert.Contains(sink.Records, record =>
            record.Level == FlowLogLevel.Information &&
            record.Message.Contains("开始执行流程"));
    }

    /// <summary>验证执行失败通过结构化 sink 记录为错误等级。</summary>
    [Fact]
    public async Task RunAsync_FailureIsWrittenAtErrorLevel()
    {
        var sink = new RecordingFlowLogSink();
        var service = TestFlowEditorFactory.CreateService(flowLogSink: sink);
        var start = service.Canvas.AddNode(new StartNode());
        var failure = service.Canvas.AddNode(new FailNode { Name = "Failure" });
        service.Connections.Add(Flow(start, failure));

        await service.Execution.RunAsync(_ => { });

        Assert.Contains(sink.Records, record =>
            record.Level == FlowLogLevel.Error &&
            record.Message.Contains("错误"));
    }

    /// <summary>验证流程执行前的校验警告也会进入结构化日志，不仅显示在输出栏。</summary>
    [Fact]
    public async Task RunAsync_ValidationWarningIsWrittenAtWarningLevel()
    {
        var sink = new RecordingFlowLogSink();
        var service = TestFlowEditorFactory.CreateService(flowLogSink: sink);
        service.Canvas.AddNode(new StartNode());
        service.Canvas.AddNode(new CountingNode { Name = "Orphan" });

        await service.Execution.RunAsync(_ => { });

        Assert.Contains(sink.Records, record =>
            record.Level == FlowLogLevel.Warning &&
            record.Message.Contains("[校验][警告]"));
    }

    /// <summary>验证单步执行失败与完整流程使用同一错误等级。</summary>
    [Fact]
    public async Task StepContinueAsync_FailureIsWrittenAtErrorLevel()
    {
        var sink = new RecordingFlowLogSink();
        var service = TestFlowEditorFactory.CreateService(flowLogSink: sink);
        var failure = service.Canvas.AddNode(new FailNode { Name = "StepFailure" });

        await service.Execution.StepContinueAsync(failure.Model, _ => { });

        Assert.Contains(sink.Records, record =>
            record.NodeName == null &&
            record.Level == FlowLogLevel.Error &&
            record.Message.Contains("[单步][错误]"));
    }

    /// <summary>
    /// 验证 Stop_CancelsRunningNodeAndCompletesRunTask 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Stop_CancelsRunningNodeAndCompletesRunTask()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var model = new CancellableNode { Name = "Waiting" };
        var waiting = service.Canvas.AddNode(model);
        service.Connections.Add(Flow(start, waiting));

        var runTask = service.Execution.RunAsync(_ => { });
        await model.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(service.Model.IsRunning);

        service.Execution.Stop();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("已取消", service.Model.StatusText);
        Assert.False(service.Model.IsRunning);
        Assert.False(service.Model.IsPaused);
    }

    /// <summary>
    /// 验证 StopLoop_CompletesLoopAndReportsStoppedState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunLoopAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StopLoop_CompletesLoopAndReportsStoppedState()
    {
        var service = CreateService();
        service.Model.LoopInterval = 10;
        var start = service.Canvas.AddNode(new StartNode());
        var model = new CountingNode { Name = "Worker" };
        var worker = service.Canvas.AddNode(model);
        service.Connections.Add(Flow(start, worker));

        var loopTask = service.Execution.RunLoopAsync(_ => { });
        await WaitUntilAsync(() => Volatile.Read(ref model.Executions) >= 2);
        service.Execution.StopLoop();
        await loopTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(model.Executions >= 2);
        Assert.Equal("循环执行已停止", service.Model.StatusText);
        Assert.False(service.Model.IsLoopRunning);
        Assert.False(service.Model.IsRunning);
    }

    /// <summary>
    /// 验证 AppendLog_FlushesQuietTrailingBatchWithoutAnotherMessage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、Append、DoesNotContain、WaitUntilAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task AppendLog_FlushesQuietTrailingBatchWithoutAnotherMessage()
    {
        var service = CreateService();

        service.Output.Append("first");
        service.Output.Append("last-before-silence");

        Assert.DoesNotContain("last-before-silence", service.Model.OutputText);

        await WaitUntilAsync(() => service.Model.OutputText.Contains("last-before-silence"));

        Assert.Contains("first", service.Model.OutputText);
        Assert.Contains("last-before-silence", service.Model.OutputText);
    }

    /// <summary>
    /// 验证 RunAsync_PublishesNodeStartBeforeLongRunningNodeCompletes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RunAsync_PublishesNodeStartBeforeLongRunningNodeCompletes()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var model = new CancellableNode { Name = "Waiting" };
        var waiting = service.Canvas.AddNode(model);
        service.Connections.Add(Flow(start, waiting));

        var runTask = service.Execution.RunAsync(service.Output.Append);
        await model.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await WaitUntilAsync(() =>
            service.Model.OutputText.Contains("[Waiting] [开始]"));

        service.Execution.Stop();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 验证 RunAsync_LogSinkFailureAfterSuccessfulNodes_DoesNotChangeSuccessStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、RunAsync、Equal、All；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RunAsync_LogSinkFailureAfterSuccessfulNodes_DoesNotChangeSuccessStatus()
    {
        var service = TestFlowEditorFactory.CreateService(flowLogSink: new ThrowingFlowLogSink());
        service.Canvas.AddNode(new StartNode());

        await service.Execution.RunAsync(service.Output.Append);

        Assert.Equal("执行完成", service.Model.StatusText);
        Assert.All(service.Nodes, node => Assert.Equal(NodeStatus.Success, node.Status));
    }

    /// <summary>
    /// 验证 RunAsync_ImmediatelyReplacesPreviousErrorWithRunningStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RunAsync_ImmediatelyReplacesPreviousErrorWithRunningStatus()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var model = new CancellableNode { Name = "Waiting" };
        var waiting = service.Canvas.AddNode(model);
        service.Connections.Add(Flow(start, waiting));
        service.Model.StatusText = "执行出错";

        var runTask = service.Execution.RunAsync(service.Output.Append);
        await model.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("正在执行...", service.Model.StatusText);

        service.Execution.Stop();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 验证 CreateService 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService；具体条件和先后次序见方法体。
    /// </summary>
    private static FlowChartService CreateService() =>
        TestFlowEditorFactory.CreateService();

    /// <summary>
    /// 验证 WaitUntilAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromSeconds、condition、Delay；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, cts.Token);
    }
}
