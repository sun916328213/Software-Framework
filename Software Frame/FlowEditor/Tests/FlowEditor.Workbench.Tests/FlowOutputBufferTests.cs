// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowOutputBufferTests、RecordingUiDispatcher。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Interfaces、FlowEditor.Controls.Services、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Services;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证输出深模块的缓冲、截断、节流与调度边界。</summary>
public sealed class FlowOutputBufferTests
{
    /// <summary>验证发布动作始终通过 UI 调度 seam 执行。</summary>
    [Fact]
    public void Append_PublishesThroughUiDispatcher()
    {
        var dispatcher = new RecordingUiDispatcher();
        string? published = null;
        var buffer = new FlowOutputBuffer(text => published = text, dispatcher);

        buffer.Append("hello");

        Assert.Equal(1, dispatcher.InvocationCount);
        Assert.Contains("hello", published);
    }

    /// <summary>验证超过容量后会丢弃旧内容并保留最近日志。</summary>
    [Fact]
    public void Flush_WhenCapacityExceeded_DropsOldContentAndKeepsRecentContent()
    {
        string? published = null;
        var buffer = new FlowOutputBuffer(
            text => published = text,
            ImmediateUiDispatcher.Instance,
            maxChars: 120,
            flushInterval: TimeSpan.Zero);

        buffer.Append($"old-{new string('a', 40)}");
        buffer.Append($"middle-{new string('b', 40)}");
        buffer.Append($"recent-{new string('c', 40)}");
        buffer.Flush();

        Assert.DoesNotContain("old-", published);
        Assert.Contains("recent-", published);
    }

    /// <summary>验证没有后续消息时，节流窗口末尾仍会发布最后一批日志。</summary>
    [Fact]
    public async Task Append_AfterImmediateFlush_PublishesQuietTrailingBatch()
    {
        var trailingPublished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var buffer = new FlowOutputBuffer(
            text =>
            {
                if (text.Contains("trailing", StringComparison.Ordinal))
                    trailingPublished.TrySetResult();
            },
            ImmediateUiDispatcher.Instance,
            flushInterval: TimeSpan.FromMilliseconds(20));

        buffer.Append("first");
        buffer.Append("trailing");

        await trailingPublished.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>验证清空后，先前排队的节流刷新不会把旧日志重新发布。</summary>
    [Fact]
    public async Task Clear_WithScheduledFlush_DoesNotRestoreOldOutput()
    {
        string? published = null;
        var buffer = new FlowOutputBuffer(
            text => published = text,
            ImmediateUiDispatcher.Instance,
            flushInterval: TimeSpan.FromMilliseconds(30));

        buffer.Append("old-output");
        buffer.Append("queued-output");
        buffer.Clear();
        await Task.Delay(80);

        Assert.Equal(string.Empty, published);
    }

    /// <summary>验证 ViewModel 命令会同时清空服务缓冲和界面绑定文本。</summary>
    [Fact]
    public void ClearOutputCommand_ClearsCurrentTabOutput()
    {
        var service = TestFlowEditorFactory.CreateService();
        var viewModel = TestFlowEditorFactory.CreateViewModel(service);
        service.Output.Append("visible-output");
        Assert.Contains("visible-output", service.Model.OutputText);

        viewModel.ClearOutputCommand.Execute(null);

        Assert.Equal(string.Empty, service.Model.OutputText);
    }

    /// <summary>记录调度次数并在当前线程执行动作的测试 adapter。</summary>
    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        /// <summary>已接收的调度次数。</summary>
        public int InvocationCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 action；具体条件和先后次序见方法体。
        /// </remarks>
        public void Invoke(Action action)
        {
            InvocationCount++;
            action();
        }
    }
}
