using FlowEditor.Interfaces;
using FlowEditor.Services;
using Xunit;

namespace FlowEditorTest;

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
        service.AppendLog("visible-output");
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
        public void Invoke(Action action)
        {
            InvocationCount++;
            action();
        }
    }
}
