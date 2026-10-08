using Communication.Core.Abstractions;
using Communication.Core.Protocols.Http;

namespace Communication.Runtime.Protocols.Http;

/// <summary>串行管理监听器启动/停止；路由和接收任务继续由具体协议负责。</summary>
internal sealed class HttpServerLifecycle(
    IHttpServerListener listener, object device, Func<bool> isDisposed,
    Action<bool, string?> stateChanged, Action<Exception> reportError) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    internal ConnectionState State { get; set; } = ConnectionState.Disconnected;

    /// <summary>串行准备路由、启动监听并通知连接状态。</summary>
    internal async Task<bool> ConnectAsync(CancellationToken ct,
        Action prepare, Action startReceiveLoop, string message)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(isDisposed(), device);
            if (State == ConnectionState.Connected) return true;
            State = ConnectionState.Connecting;
            prepare();
            listener.Start();
            startReceiveLoop();
            State = ConnectionState.Connected;
            stateChanged(true, message);
            return true;
        }
        catch (OperationCanceledException)
        {
            State = ConnectionState.Disconnected;
            throw;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Faulted;
            reportError(ex);
            stateChanged(false, ex.Message);
            return false;
        }
        finally { _gate.Release(); }
    }

    /// <summary>停止监听并等待接收与请求处理结束。</summary>
    internal async Task DisconnectAsync(CancellationToken ct,
        Action cancelReceiveLoop, Func<Task> drain, string message)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (State == ConnectionState.Disconnected) return;
            State = ConnectionState.Stopping;
            cancelReceiveLoop();
            listener.Stop();
            // Stop解除挂起的GetContextAsync；等待接收和请求处理结束后才返回。
            await drain().ConfigureAwait(false);
            State = ConnectionState.Disconnected;
            stateChanged(false, message);
        }
        finally { _gate.Release(); }
    }

    /// <summary>释放拥有的监听器和生命周期锁。</summary>
    public void Dispose()
    {
        listener.Dispose();
        _gate.Dispose();
    }
}
