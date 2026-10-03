// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ZebraNetworkPrinter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Printers、Device.Core.Devices、Device.Core.Printers、Zebra.Sdk.Comm、Device.Core.Printers.PrinterStatus、Zebra.Sdk.Printer.ZebraPrinter。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Printers;
using Device.Core.Devices;
using Device.Core.Printers;
using Zebra.Sdk.Comm;
using CorePrinterStatus = Device.Core.Printers.PrinterStatus;
using ZebraPrinter = Zebra.Sdk.Printer.ZebraPrinter;
using ZebraPrinterFactory = Zebra.Sdk.Printer.ZebraPrinterFactory;
using ZebraPrinterStatus = Zebra.Sdk.Printer.PrinterStatus;

namespace Device.Runtime.Printers.Zebra;

/// <summary>
/// 通过 Zebra Link-OS SDK 访问网络 ZPL 打印机的适配器。
/// </summary>
/// <remarks>
/// Zebra SDK 提供同步阻塞接口。本适配器把阻塞调用放到线程池，并通过操作锁保证
/// 同一个 SDK 连接不会被连接、状态查询和打印操作并发访问。
/// </remarks>
internal sealed class ZebraNetworkPrinter : IPrinter
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private Connection? _connection;
    private ZebraPrinter? _printer;
    private int _disposeStarted;

    /// <summary>使用已经由工厂验证的配置创建未连接打印机。</summary>
    /// <param name="options">打印机配置快照。</param>
    public ZebraNetworkPrinter(PrinterOptions options)
    {
        Options = options;
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    /// <inheritdoc />
    public PrinterOptions Options { get; }

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、Ok、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Connection? connection = null;
        try
        {
            ThrowIfDisposed();
            if (State == DeviceState.Connected)
                return DeviceResult.Ok();

            if (_connection is not null)
            {
                // 上一次断开可能在 SDK Close 中失败。重连前先完成旧连接清理，
                // 避免覆盖唯一引用后遗留无法再次关闭的网络连接。
                await Task.Run(_connection.Close, CancellationToken.None).ConfigureAwait(false);
                _connection = null;
                _printer = null;
            }

            State = DeviceState.Connecting;
            connection = new TcpConnection(
                Options.IpAddress,
                Options.Port,
                Options.MaximumReadTimeoutMilliseconds,
                Options.TimeToWaitForMoreDataMilliseconds);

            // SDK 没有异步打开接口。传入的令牌只能取消尚未开始的线程池任务，
            // 已经进入 SDK 的阻塞 Open 调用后不能强制中断底层套接字连接。
            await Task.Run(connection.Open, cancellationToken).ConfigureAwait(false);
            var printer = await Task.Run(
                () => ZebraPrinterFactory.GetInstance(connection),
                cancellationToken).ConfigureAwait(false);

            _connection = connection;
            _printer = printer;
            connection = null;
            State = DeviceState.Connected;
            return DeviceResult.Ok();
        }
        catch (ObjectDisposedException)
        {
            await CloseSilentlyAsync(connection).ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException)
        {
            await CloseSilentlyAsync(connection).ConfigureAwait(false);
            State = DeviceState.Disconnected;
            throw;
        }
        catch (Exception exception)
        {
            await CloseSilentlyAsync(connection).ConfigureAwait(false);
            State = DeviceState.Faulted;
            return DeviceResult.Failure("printer.zebra.connect_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、Ok、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_connection is null)
            {
                _printer = null;
                State = DeviceState.Disconnected;
                return DeviceResult.Ok();
            }

            var connection = _connection;

            // 清理资源一旦开始就不应被调用方取消，否则可能遗留打开的 SDK 连接。
            // 只有关闭成功后才清空字段；如果 SDK 抛出异常，保留引用供后续再次断开。
            await Task.Run(connection.Close, CancellationToken.None).ConfigureAwait(false);
            _connection = null;
            _printer = null;
            State = DeviceState.Disconnected;
            return DeviceResult.Ok();
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure("printer.zebra.disconnect_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 GetStatusAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、Failure、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult<CorePrinterStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State != DeviceState.Connected || _printer is null)
            {
                return DeviceResult<CorePrinterStatus>.Failure(
                    "printer.zebra.not_connected",
                    $"打印机 '{Name}' 尚未连接。");
            }

            // 一次调用取得完整快照，避免为每个状态字段重复查询真实打印机。
            ZebraPrinterStatus status = await Task.Run(
                _printer.GetCurrentStatus,
                cancellationToken).ConfigureAwait(false);
            return DeviceResult<CorePrinterStatus>.Ok(new CorePrinterStatus(
                ReadyToPrint: status.isReadyToPrint,
                Paused: status.isPaused,
                HeadOpen: status.isHeadOpen,
                PaperOut: status.isPaperOut,
                RibbonOut: status.isRibbonOut,
                ReceiveBufferFull: status.isReceiveBufferFull,
                HeadTooHot: status.isHeadTooHot,
                HeadCold: status.isHeadCold,
                PartialFormatInProgress: status.isPartialFormatInProgress,
                LabelsRemainingInBatch: status.labelsRemainingInBatch,
                FormatsInReceiveBuffer: status.numberOfFormatsInReceiveBuffer,
                LabelLengthInDots: status.labelLengthInDots));
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // SDK 查询失败后无法确认连接是否仍可用，要求调用方断开或重连后再操作。
            State = DeviceState.Faulted;
            return DeviceResult<CorePrinterStatus>.Failure(
                "printer.zebra.status_failed",
                exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 PrintRawAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfDisposed、Failure、ToArray；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<DeviceResult> PrintRawAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (data.IsEmpty)
                return DeviceResult.Failure("printer.zebra.print_data_empty", "打印数据不能为空。");

            if (State != DeviceState.Connected || _connection is null)
            {
                return DeviceResult.Failure(
                    "printer.zebra.not_connected",
                    $"打印机 '{Name}' 尚未连接。");
            }

            // 复制调用方内存，防止异步线程池任务执行时原缓冲区已被复用或修改。
            var bytes = data.ToArray();
            await Task.Run(() => _connection.Write(bytes), cancellationToken).ConfigureAwait(false);
            return DeviceResult.Ok();
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 写入可能只完成了部分数据，继续复用连接可能造成下一份 ZPL 与残包拼接。
            State = DeviceState.Faulted;
            return DeviceResult.Failure("printer.zebra.print_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Exchange、ConfigureAwait、WaitAsync、Run、Write；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is not null)
                await Task.Run(_connection.Close, CancellationToken.None).ConfigureAwait(false);

            _connection = null;
            _printer = null;
            State = DeviceState.Disconnected;
        }
        catch
        {
            // SDK 关闭失败时保留连接引用，并允许调用方再次尝试释放。
            State = DeviceState.Faulted;
            Volatile.Write(ref _disposeStarted, 0);
            throw;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseSilentlyAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、Run；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static async ValueTask CloseSilentlyAsync(Connection? connection)
    {
        if (connection is null)
            return;

        try
        {
            await Task.Run(connection.Close).ConfigureAwait(false);
        }
        catch
        {
            // 连接失败时保留最初的 SDK 异常，关闭阶段异常不覆盖根因。
        }
    }

    /// <summary>对象开始释放后拒绝新的设备操作。</summary>
    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
}
