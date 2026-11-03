using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Tools.Execution;

/// <summary>上下文独占和关闭事务；取消等待不能提前释放仍被原生调用借用的资源。</summary>
internal sealed partial class VisionEngine
{
    private async ValueTask<ContextLease> EnterAsync(VisionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);
        token.ThrowIfCancellationRequested();
        ContextState state;
        lock (_gate)
        {
            EnsureOwner(context);
            ObjectDisposedException.ThrowIf(_closing, this);
            if (!_contexts.TryGetValue(context.ContextId, out state!) || state.Closing)
            {
                throw new ObjectDisposedException(nameof(VisionContext));
            }
        }
        if (!await state.Gate.WaitAsync(0, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("同一工具上下文不能重入准备、校验或执行。");
        }

        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closing || state.Closing, this);
                return new(state, CancellationTokenSource.CreateLinkedTokenSource(token, state.Stopping.Token));
            }
        }
        catch
        {
            state.Gate.Release();
            throw;
        }
    }

    private void ForgetModel(ContextState state, IPreparedVisionModel model)
    {
        lock (_gate)
        {
            state.Models.RemoveAll(existing => ReferenceEquals(existing, model));
            _ownedModels.Remove(model);
        }
    }

    private void EnsureOwner(VisionContext context)
    {
        if (context.EngineInstanceId != Descriptor.EngineInstanceId)
        {
            throw new ArgumentException("上下文不属于本引擎实例。", nameof(context));
        }
    }

    private void ThrowIfClosing(ContextState state, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_closing || state.Closing)
        {
            throw new OperationCanceledException("上下文正在关闭，结果不再发布。", token);
        }
    }

    public ValueTask ReleaseContextAsync(VisionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ContextState? state;
        lock (_gate)
        {
            EnsureOwner(context);
            _contexts.TryGetValue(context.ContextId, out state);
        }
        return state == null ? ValueTask.CompletedTask : new(BeginClose(state).WaitAsync(cancellationToken));
    }

    private Task BeginClose(ContextState state)
    {
        TaskCompletionSource completion;
        var start = false;
        lock (_gate)
        {
            if (state.Close == null || state.Close.Task.IsFaulted)
            {
                state.Closing = true;
                state.Close = new(TaskCreationOptions.RunContinuationsAsynchronously);
                start = true;
            }
            completion = state.Close;
        }
        if (start)
        {
            _ = CloseStateAsync(state, completion);
        }

        return completion.Task;
    }

    private async Task CloseStateAsync(ContextState state, TaskCompletionSource completion)
    {
        List<Exception> errors = [];
        try
        {
            state.Stopping.Cancel();
        }
        catch (Exception error)
        {
            errors.Add(error);
        }
        await state.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            state.Active = null;
            foreach (var model in state.Models.ToArray())
            {
                try
                {
                    model.Dispose();
                    ForgetModel(state, model);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            if (errors.Count == 0)
            {
                lock (_gate)
                {
                    _contexts.Remove(state.Context.ContextId);
                }

                state.Stopping.Dispose();
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(new AggregateException("上下文关闭失败；未释放资源仍保留。", errors));
            }
        }
        catch (Exception error)
        {
            completion.TrySetException(error);
        }
        finally
        {
            state.Gate.Release();
        } // 不销毁信号量：可能存在已取得state、尚未入闸的调用。
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        ContextState[] states;
        var start = false;
        lock (_gate)
        {
            if (_dispose == null || _dispose.Task.IsFaulted)
            {
                _closing = true;
                _dispose = new(TaskCreationOptions.RunContinuationsAsynchronously);
                start = true;
            }
            completion = _dispose;
            states = _contexts.Values.ToArray();
        }
        if (start)
        {
            _ = CloseEngineAsync(states, completion);
        }

        return new(completion.Task);
    }

    private async Task CloseEngineAsync(ContextState[] states, TaskCompletionSource completion)
    {
        try
        {
            await Task.WhenAll(states.Select(BeginClose)).ConfigureAwait(false);
            List<Exception> errors = [];
            foreach (var backend in new[] { _ai, _vision }.Where(backend => backend != null))
            {
                if (_releasedBackends.Contains(backend!))
                {
                    continue;
                }

                try
                {
                    backend!.Dispose();
                    _releasedBackends.Add(backend);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            if (errors.Count != 0)
            {
                throw new AggregateException(errors);
            }

            completion.TrySetResult();
        }
        catch (Exception error)
        {
            completion.TrySetException(error);
        }
    }

    private sealed record ActiveModel(VisionModelReference Reference, IPreparedVisionModel Resource);
    private sealed class ContextState(VisionContext context)
    {
        internal readonly VisionContext Context = context;
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal readonly CancellationTokenSource Stopping = new();
        internal readonly List<IPreparedVisionModel> Models = [];
        internal ActiveModel? Active;
        internal Exception? CleanupFault;
        internal bool Closing;
        internal TaskCompletionSource? Close;
    }
    private sealed class ContextLease(ContextState state, CancellationTokenSource source) : IDisposable
    {
        internal ContextState State => state;
        internal CancellationToken Token => source.Token;
        public void Dispose()
        {
            source.Dispose();
            state.Gate.Release();
        }
    }
}
