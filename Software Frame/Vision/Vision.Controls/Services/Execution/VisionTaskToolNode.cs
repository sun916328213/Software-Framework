using System.ComponentModel;
using System.Text.Json.Serialization;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>
/// 可编辑任务实例。拥有原浏览工作区，原卡片仍独占工具与结果；流程节点只借用结果。
/// 与直接拥有 IImageTool 的 VisionToolFlowNode 不同，本适配必须遵守现有卡片的 UI/后台执行策略。
/// </summary>
public sealed class VisionTaskToolNode : NodeModel, IDisposable, IAsyncDisposable
{
    private readonly VisionBrowserWorkspace _workspace;
    private readonly IUiDispatcher _ui;
    private bool _running;
    private bool _disposed;
    private TaskCompletionSource? _executionStopped;
    private Task? _disposeTask;

    public VisionTaskToolNode(VisionBrowserCatalog catalog, ToolEntry entry, IUiDispatcher ui)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _workspace = new VisionBrowserWorkspace(catalog with { Tools = new[] { entry } });
        try
        {
            if (_workspace.Model.CurrentCard == null)
                throw new InvalidOperationException(_workspace.Model.CompareText);
            Editor = new VisionBrowserViewModel(_workspace);
            Editor.CurrentCard!.Model.PropertyChanged += CardChanged;
            Name = entry.Name;
        }
        catch { _workspace.Dispose(); throw; }
    }

    [JsonIgnore] public VisionBrowserViewModel Editor { get; }
    [JsonIgnore] public ToolResult? Result => Editor.CurrentCard?.LastResult;
    [JsonIgnore] public bool IsBusy => _running || Editor.CurrentCard?.Model.IsBusy == true;

    /// <summary>任务宿主在 UI 线程设置共同输入；原工作区仍协调图像、模板和结果失效。</summary>
    public void SetImage(Vision.Core.Imaging.VisionImage image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy) throw new InvalidOperationException("工具执行中不能更换输入。");
        _workspace.SetImage(image);
    }

    private void CardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolCardModel.LastResult) or nameof(ToolCardModel.IsBusy))
            Status = Editor.CurrentCard!.Model.IsBusy ? NodeStatus.Running
                : Result == null ? NodeStatus.Idle : Result.Success ? NodeStatus.Success : NodeStatus.Error;
        if (e.PropertyName == nameof(ToolCardModel.IsBusy)) OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>原引擎可在工作线程调度，但卡片准备/发布始终回到所属 UI 上下文。</summary>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        Task execution = Task.CompletedTask;
        _ui.Invoke(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsBusy) throw new InvalidOperationException("同一任务实例不能重复执行。");
            ct.ThrowIfCancellationRequested();
            _running = true;
            _executionStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            try { execution = Editor.CurrentCard!.RunAsync(); }
            catch
            {
                _running = false;
                _executionStopped.TrySetResult();
                throw;
            }
        });
        try
        {
            await execution.ConfigureAwait(false);
            _ui.Invoke(() =>
            {
                if (_disposed || ct.IsCancellationRequested)
                {
                    Editor.CurrentCard!.ClearResult();
                    ct.ThrowIfCancellationRequested();
                    throw new ObjectDisposedException(nameof(VisionTaskToolNode));
                }
                if (Result?.Success != true)
                    throw new InvalidOperationException($"{Name}：{Result?.Message ?? Editor.CurrentCard!.StatusText}");
            });
        }
        finally
        {
            _ui.Invoke(() =>
            {
                _running = false;
                try { OnPropertyChanged(nameof(IsBusy)); }
                finally { _executionStopped!.TrySetResult(); }
            });
        }
    }

    /// <summary>同步入口不阻塞UI；空闲释放错误仍同步上报，运行中由DisposeAsync等待并观察错误。</summary>
    public void Dispose()
    {
        var cleanup = BeginDispose();
        if (cleanup.IsCompleted) cleanup.GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync() => new(BeginDispose());

    private Task BeginDispose()
    {
        _ui.Invoke(() =>
        {
            if (_disposeTask != null) return;
            _disposed = true;
            Editor.Model.IsClosed = true;
            var card = Editor.CurrentCard;
            Exception? closeError = null;
            try { card?.BeginClose(); }
            catch (Exception error) { closeError = error; }
            // 不只等待流程调用：手动点击执行、直接调用卡片API也由卡片记录。
            _disposeTask = ReleaseWhenIdleAsync(_executionStopped?.Task ?? Task.CompletedTask,
                card?.ExecutionTask ?? Task.CompletedTask, closeError);
        });
        return _disposeTask!;
    }

    private async Task ReleaseWhenIdleAsync(Task nodeStopped, Task cardExecution, Exception? closeError)
    {
        List<Exception> errors = [];
        if (closeError != null) errors.Add(closeError);
        try { await Task.WhenAll(nodeStopped, cardExecution).ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        _ui.Invoke(() =>
        {
            if (Editor.CurrentCard != null) Editor.CurrentCard.Model.PropertyChanged -= CardChanged;
            try { Editor.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            try { _workspace.Dispose(); }
            catch (Exception error) { errors.Add(error); }
        });
        if (errors.Count > 0) throw new AggregateException($"{Name}关闭失败。", errors);
    }
}
