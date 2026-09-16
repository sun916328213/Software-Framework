using System.Collections.Concurrent;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>从 FlowChartService 运行入口到执行引擎的集成回归。</summary>
[Collection(FlowLogContextCollection.Name)]
public class FlowChartServiceExecutionTests
{
    private sealed class RecordingFlowLogSink : IFlowLogSink
    {
        /// <summary>线程安全记录收到的结构化日志。</summary>
        public ConcurrentQueue<FlowLogRecord> Records { get; } = new();

        /// <inheritdoc />
        public void Write(FlowLogRecord record) => Records.Enqueue(record);
    }

    private sealed class ThrowingFlowLogSink : IFlowLogSink
    {
        /// <inheritdoc />
        public void Write(FlowLogRecord record)
        {
            if (record.Message.Contains("执行完成"))
                throw new System.IO.IOException("日志磁盘不可用");
        }
    }

    private sealed class CancellableNode : NodeModel
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }

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

    [Fact]
    public async Task RunAsync_LogSinkFailureAfterSuccessfulNodes_DoesNotChangeSuccessStatus()
    {
        var service = TestFlowEditorFactory.CreateService(flowLogSink: new ThrowingFlowLogSink());
        service.Canvas.AddNode(new StartNode());

        await service.Execution.RunAsync(service.Output.Append);

        Assert.Equal("执行完成", service.Model.StatusText);
        Assert.All(service.Nodes, node => Assert.Equal(NodeStatus.Success, node.Status));
    }

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

    private static FlowChartService CreateService() =>
        TestFlowEditorFactory.CreateService();

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, cts.Token);
    }
}
