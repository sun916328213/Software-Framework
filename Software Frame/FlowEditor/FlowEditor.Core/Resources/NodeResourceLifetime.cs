// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeResourceOwnership、NodeResourceLease、NodeResourceScope。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>
/// 资源所有权语义（P0）。第一阶段规则：
/// Runtime 统一管理跨节点输出资源，Node 默认只借用输入；
/// 对确需独占的 SDK 对象（Halcon Tool、Camera Handle）使用显式 <see cref="NodeResourceLease{T}"/>。
/// </summary>
public enum NodeResourceOwnership
{
    /// <summary>借用：不拥有、不释放。默认值；节点不得 Dispose 上游共享输入。</summary>
    Borrowed = 0,

    /// <summary>共享：多分支只读共享，由 Runtime 在分支结束/流程取消/节点失败时统一清理。</summary>
    Shared = 1,

    /// <summary>独占：节点创建并拥有，必须通过 Lease 或 Scope 明确释放。</summary>
    Owned = 2,

    /// <summary>克隆：节点自行复制后拥有副本，下游修改不影响上游。</summary>
    Clone = 3,
}

/// <summary>
/// 独占资源的显式租约（P0）。用于 Halcon Tool、Camera/Device Session 等长生命周期或非托管资源。
/// 谁创建、谁拥有、谁释放：创建方返回 Lease，消费方 Dispose 即归还；流程取消/失败/分支结束时
/// 未释放的 Lease 由 <see cref="NodeResourceScope"/> 统一清理。
/// </summary>
/// <typeparam name="T">被管理的资源类型。</typeparam>
public sealed class NodeResourceLease<T> : IDisposable
{
    private readonly Action<T>? _onRelease;
    private int _disposed;

    /// <summary>使用资源值与释放回调创建租约。</summary>
    /// <param name="resourceKey">资源键，如 "Camera:Cam01"、"VisionTool:Blob01"（P1 并发调度复用）。</param>
    /// <param name="value">被管理的资源。</param>
    /// <param name="ownership">所有权语义。</param>
    /// <param name="onRelease">释放回调（幂等，只执行一次）。</param>
    public NodeResourceLease(string resourceKey, T value, NodeResourceOwnership ownership, Action<T>? onRelease = null)
    {
        ResourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
        Value = value;
        Ownership = ownership;
        _onRelease = onRelease;
    }

    /// <summary>资源键。</summary>
    public string ResourceKey { get; }

    /// <summary>被管理的资源。</summary>
    public T Value { get; }

    /// <summary>所有权语义。</summary>
    public NodeResourceOwnership Ownership { get; }

    /// <summary>是否已释放。</summary>
    public bool IsDisposed => _disposed != 0;

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Exchange、Dispose；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _onRelease?.Invoke(Value); }
        catch { /* 释放路径不抛异常，由调用方日志记录 */ }
        if (Value is IDisposable disposable && Ownership is NodeResourceOwnership.Owned or NodeResourceOwnership.Clone)
        {
            try { disposable.Dispose(); }
            catch { /* 同上 */ }
        }
    }
}

/// <summary>
/// 单次节点执行/单分支的资源作用域（P0）。节点创建的 Owned/Clone 资源在此登记，
/// 流程取消、节点失败、分支结束时统一清理，避免泄漏或重复 Dispose 上游输入。
/// </summary>
public sealed class NodeResourceScope : IDisposable
{
    private readonly List<IDisposable> _leases = [];
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>登记一个租约，作用域释放时统一清理。</summary>
    public NodeResourceLease<T> Track<T>(NodeResourceLease<T> lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _leases.Add(lease);
        }
        return lease;
    }

    /// <summary>登记任意一次性资源。</summary>
    public T Track<T>(T disposable) where T : IDisposable
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _leases.Add(disposable);
        }
        return disposable;
    }

    /// <summary>当前登记数量（Workbench 资源监控使用）。</summary>
    public int Count
    {
        get { lock (_gate) return _leases.Count; }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Clear、Dispose；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public void Dispose()
    {
        List<IDisposable> snapshot;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            snapshot = [.. _leases];
            _leases.Clear();
        }
        foreach (var lease in snapshot)
        {
            try { lease.Dispose(); }
            catch { /* 清理路径不抛异常 */ }
        }
    }
}
