using Device.Core.Devices;
using Device.Core.FrameGrabbers;

namespace Device.Drivers.FrameGrabbers;

/// <summary>
/// 采集卡适配器共享的生命周期、错误转换和安全事件发布实现。
/// </summary>
internal abstract class FrameGrabberBase : IFrameGrabber
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private int _disposeStarted;

    /// <summary>使用已经由工厂验证的配置创建采集卡。</summary>
    /// <param name="options">采集卡配置快照。</param>
    protected FrameGrabberBase(FrameGrabberOptions options)
    {
        Options = options;
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    /// <inheritdoc />
    public FrameGrabberOptions Options { get; }

    /// <inheritdoc />
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    /// <summary>普通失败使用的稳定错误码前缀。</summary>
    protected abstract string ErrorCodePrefix { get; }

    /// <summary>加载厂商驱动、分配缓冲并开始连续采集。</summary>
    protected abstract void ConnectCore();

    /// <summary>停止连续采集并释放全部厂商资源。</summary>
    protected abstract void DisconnectCore();

    /// <inheritdoc />
    public async ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
            if (State == DeviceState.Connected)
                return DeviceResult.Ok();

            State = DeviceState.Connecting;
            // 两个厂商 SDK 都只提供同步初始化接口，放入线程池避免阻塞调用线程。
            await Task.Run(ConnectCore, cancellationToken).ConfigureAwait(false);
            State = DeviceState.Connected;
            return DeviceResult.Ok();
        }
        catch (OperationCanceledException)
        {
            CloseAfterFailure();
            State = DeviceState.Disconnected;
            throw;
        }
        catch (Exception exception)
        {
            CloseAfterFailure();
            State = DeviceState.Faulted;
            return DeviceResult.Failure($"{ErrorCodePrefix}.connect_failed", exception.Message);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == DeviceState.Disconnected)
                return DeviceResult.Ok();

            // 资源清理一旦开始就必须完成，调用方取消不能遗留原生缓冲区或回调。
            await Task.Run(DisconnectCore, CancellationToken.None).ConfigureAwait(false);
            State = DeviceState.Disconnected;
            return DeviceResult.Ok();
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure($"{ErrorCodePrefix}.disconnect_failed", exception.Message);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        await DisconnectAsync().ConfigureAwait(false);
        _lifecycleGate.Dispose();
    }

    /// <summary>向全部订阅者发布已经复制完成的托管帧。</summary>
    /// <param name="frame">不再依赖厂商缓冲区生命周期的帧。</param>
    protected void PublishFrame(FrameGrabberFrame frame)
    {
        var handlers = FrameReceived?.GetInvocationList();
        if (handlers is null)
            return;

        var eventArgs = new FrameReceivedEventArgs(frame);
        foreach (EventHandler<FrameReceivedEventArgs> handler in handlers)
        {
            try
            {
                handler(this, eventArgs);
            }
            catch
            {
                // 一个业务订阅者异常不能中断厂商采集回调或影响其他订阅者。
            }
        }
    }

    private void CloseAfterFailure()
    {
        try
        {
            DisconnectCore();
        }
        catch
        {
            // 保留最初的初始化异常作为对外诊断信息。
        }
    }
}
