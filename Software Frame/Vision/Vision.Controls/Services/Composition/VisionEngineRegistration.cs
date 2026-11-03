#nullable enable
using Prism.Ioc;
using Vision.Core;
using Vision.Core.Engine;
using Vision.Tools;

namespace Vision.Controls;

/// <summary>可选的新引擎装配；不注册页面、不创建会话、不在注册/空关闭时加载SDK。</summary>
public static class VisionEngineRegistration
{
    /// <summary>冻结选择并注册唯一引擎；宿主必须在借用页面/节点停止后等待返回句柄的DisposeAsync。</summary>
    public static IAsyncDisposable RegisterVisionEngine(this IContainerRegistry registry, VisionEngineSelection selection)
        => RegisterVisionEngine(registry, selection, VisionEngineFactory.Create);

    internal static IAsyncDisposable RegisterVisionEngine(IContainerRegistry registry, VisionEngineSelection selection,
        Func<VisionEngineSelection, IVisionEngine> create)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(create);
        VisionEngineFactory.ValidateSelection(selection);
        if (registry.IsRegistered<IVisionEngine>()) throw new InvalidOperationException("宿主已注册IVisionEngine，禁止覆盖或创建第二套引擎。");
        var owner = new EngineOwner(() => create(selection));
        // 每次解析均经关闭守卫，实例本身由owner内的Lazy唯一持有，不交给容器创建第二份。
        registry.Register<IVisionEngine>(_ => owner.Resolve());
        return owner;
    }

    private sealed class EngineOwner(Func<IVisionEngine> create) : IAsyncDisposable
    {
        private readonly object _gate = new();
        private readonly Lazy<IVisionEngine> _engine = new(() => create() ?? throw new InvalidOperationException("引擎工厂返回空值。"),
            LazyThreadSafetyMode.ExecutionAndPublication);
        private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _resolving;
        private bool _closing;
        private TaskCompletionSource? _close;

        internal IVisionEngine Resolve()
        {
            lock (_gate) { ObjectDisposedException.ThrowIf(_closing, this); _resolving++; }
            try
            {
                var engine = _engine.Value; // SDK同步构造不在状态锁内；Lazy保证只有一次初始化。
                lock (_gate) { ObjectDisposedException.ThrowIf(_closing, this); return engine; }
            }
            finally
            {
                lock (_gate) { if (--_resolving == 0 && _closing) _drained.TrySetResult(); }
            }
        }

        public ValueTask DisposeAsync()
        {
            TaskCompletionSource completion;
            var start = false;
            lock (_gate)
            {
                _closing = true;
                if (_resolving == 0) _drained.TrySetResult();
                if (_close == null || _close.Task.IsFaulted)
                {
                    _close = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    start = true;
                }
                completion = _close;
            }
            if (start) _ = CloseAsync(completion); // 不持状态锁调用SDK；失败句柄保留，可再次等待/重试。
            return new(completion.Task);
        }

        private async Task CloseAsync(TaskCompletionSource completion)
        {
            try
            {
                await _drained.Task.ConfigureAwait(false);
                if (_engine.IsValueCreated) await _engine.Value.DisposeAsync().ConfigureAwait(false);
                completion.TrySetResult();
            }
            catch (Exception error) { completion.TrySetException(error); }
        }
    }
}
