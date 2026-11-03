using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core;
using FlowEditor.Core.Nodes;
using Vision.Core;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.FlowNode;

/// <summary>
/// 新契约流程桥：借用冻结的引擎，独占并延迟创建一个上下文。
/// 不拥有输入/结果托管快照，不训练模型，不保存引擎、图像或执行结果到文档。
/// </summary>
public abstract class VisionOperationNode<TResult> : NodeModel, INodeDocumentState, IAsyncDisposable
    where TResult : class
{
    private readonly IVisionEngine _engine;
    private readonly SemaphoreSlim _execution = new(1, 1);
    private readonly object _closeGate = new();
    private VisionContext? _context;
    private volatile bool _closing;
    private Task? _closeTask;
    private TResult? _result;

    protected VisionOperationNode(IVisionEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        AddCustomOutputPort(nameof(Result), nameof(Result));
    }

    /// <summary>本次强类型快照；NoMatch可以有值，失败/取消/配置修改后无值。</summary>
    [JsonIgnore, NodeOutput]
    public TResult? Result => _result;

    /// <summary>算法状态与流程状态分离；NoMatch及有效NG均不阻断下游判定。</summary>
    [JsonIgnore, NodeRuntimeState]
    public VisionExecutionStatus? LastExecutionStatus { get; private set; }

    /// <summary>本次执行诊断，不使用上次成功快照冒充失败结果。</summary>
    [JsonIgnore, NodeRuntimeState]
    public VisionIssue? LastIssue { get; private set; }

    protected abstract VisionOperation<TResult> CreateOperation();
    protected virtual void ReadFlowInputs() { }

    /// <summary>图执行严格读取本次端口，包括null；不沿用基类跳过null后遗留的输入属性。</summary>
    public sealed override Task ExecuteAsync(NodeExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ReadFlowInputs();
        return ExecuteAsync(ctx.CancellationToken);
    }

    public sealed override async Task ExecuteAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        if (!_execution.Wait(0))
        {
            throw new InvalidOperationException("视觉节点正在执行或释放，不能重入。");
        }

        try
        {
            ClearPublished();
            ObjectDisposedException.ThrowIf(_closing, this);
            ct.ThrowIfCancellationRequested();
            var operation = CreateOperation();
            Status = NodeStatus.Running;
            _context ??= await _engine.CreateContextAsync(ct);
            var outcome = await _engine.ExecuteAsync(_context, operation, ct);
            ct.ThrowIfCancellationRequested();
            lock (_closeGate)
            {
                if (_closing)
                {
                    throw new OperationCanceledException("节点关闭期间的迟到结果不发布。");
                }

                LastExecutionStatus = outcome.Status;
                LastIssue = outcome.Issue;
                if (outcome.Value == null)
                {
                    throw new InvalidOperationException(outcome.Issue?.Message ?? "视觉执行没有有效结果。");
                }

                _result = outcome.Value;
            }
            Status = NodeStatus.Success;
        }
        catch (OperationCanceledException)
        {
            LastExecutionStatus = VisionExecutionStatus.Cancelled;
            LastIssue ??= new VisionIssue("NodeExecutionCancelled", "节点取消或关闭，本次结果未发布。");
            Status = NodeStatus.Idle;
            throw;
        }
        catch (Exception error)
        {
            LastExecutionStatus ??= VisionExecutionStatus.Failed;
            LastIssue ??= new VisionIssue("NodeExecutionFailed", error.Message);
            Status = NodeStatus.Error;
            throw;
        }
        finally
        {
            try
            {
                NotifyPublished();
            }
            finally
            {
                _execution.Release();
            }
        }
    }

    /// <summary>配置和输入只在空闲时修改；修改立即撤销节点输出及端口旧值。</summary>
    protected void SetInput<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        if (!_execution.Wait(0))
        {
            throw new InvalidOperationException("视觉节点执行期间不能修改输入或配置。");
        }

        try
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                OnPropertyChanging(name);
                field = value;
                // 通知新配置前先撤销旧结果，订阅者不能观察到“新配方+旧快照”。
                ClearPublished();
                OnPropertyChanged(name);
            }
        }
        finally
        {
            _execution.Release();
        }
    }

    private void ClearPublished()
    {
        _result = null;
        LastExecutionStatus = null;
        LastIssue = null;
        Status = NodeStatus.Idle;
        NotifyPublished();
    }

    private void NotifyPublished()
    {
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(LastExecutionStatus));
        OnPropertyChanged(nameof(LastIssue));
        OnPropertyChanged("OutputImage");
        OnPropertyChanged("Passed");
        SyncOutputValues();
    }

    public abstract Dictionary<string, object?> CaptureDocumentState();
    public abstract void RestoreDocumentState(Dictionary<string, object?> state);

    /// <summary>关闭不可撤回；共享Task而非重复消费ValueTask，清理失败后保留上下文供重试。</summary>
    public ValueTask DisposeAsync()
    {
        lock (_closeGate)
        {
            _closing = true;
            if (_closeTask == null || _closeTask.IsFaulted || _closeTask.IsCanceled)
            {
                _closeTask = CloseAsync();
            }

            return new ValueTask(_closeTask);
        }
    }

    private async Task CloseAsync()
    {
        // 先让出同步关闭锁；锁内不调用外部引擎或属性通知。
        await Task.Yield();
        await _execution.WaitAsync();
        try
        {
            ClearPublished();
            if (_context != null)
            {
                await _engine.ReleaseContextAsync(_context, CancellationToken.None);
                _context = null;
            }
        }
        finally
        {
            _execution.Release();
        }
    }
}
