// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：FrameGrabberBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.FrameGrabbers。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.FrameGrabbers;

namespace Device.Runtime.FrameGrabbers;

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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIf、Read、Ok；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Ok、Run、Failure；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Exchange、ConfigureAwait、DisconnectAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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

    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseAfterFailure。
    /// 正文中的关键协作调用包括 DisconnectCore；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
