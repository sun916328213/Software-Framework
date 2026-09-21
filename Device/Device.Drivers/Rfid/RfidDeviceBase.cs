using System.Text;
using Device.Core.Devices;
using Device.Core.Rfid;

namespace Device.Drivers.Rfid;

/// <summary>
/// RFID 设备共享的生命周期、页参数检查和错误转换实现。
/// </summary>
internal abstract class RfidDeviceBase : IRfidDevice
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private int _disposeStarted;

    /// <summary>使用不可变配置创建 RFID 适配器。</summary>
    /// <param name="options">已经由工厂验证的配置。</param>
    protected RfidDeviceBase(RfidOptions options)
    {
        Options = options;
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    /// <inheritdoc />
    public RfidOptions Options { get; }

    /// <summary>普通失败使用的稳定错误码前缀。</summary>
    protected abstract string ErrorCodePrefix { get; }

    /// <summary>建立协议连接。</summary>
    protected abstract ValueTask<bool> ConnectCoreAsync(CancellationToken cancellationToken);

    /// <summary>断开协议连接。</summary>
    protected abstract ValueTask DisconnectCoreAsync(CancellationToken cancellationToken);

    /// <summary>执行厂商协议的连续页读取。</summary>
    protected abstract ValueTask<string> ReadPagesCoreAsync(
        int startPage,
        int pageCount,
        CancellationToken cancellationToken);

    /// <summary>执行厂商协议的连续页写入。</summary>
    protected abstract ValueTask WritePagesCoreAsync(
        int startPage,
        IReadOnlyList<string> pages,
        CancellationToken cancellationToken);

    /// <summary>释放协议客户端拥有的资源。</summary>
    protected abstract ValueTask DisposeCoreAsync();

    /// <inheritdoc />
    public async ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
            if (State == DeviceState.Connected)
                return DeviceResult.Ok();

            State = DeviceState.Connecting;
            if (await ConnectCoreAsync(cancellationToken).ConfigureAwait(false))
            {
                State = DeviceState.Connected;
                return DeviceResult.Ok();
            }

            State = DeviceState.Faulted;
            return DeviceResult.Failure($"{ErrorCodePrefix}.connect_failed", "RFID 连接失败。");
        }
        catch (OperationCanceledException)
        {
            State = DeviceState.Disconnected;
            throw;
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure($"{ErrorCodePrefix}.connect_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == DeviceState.Disconnected)
                return DeviceResult.Ok();

            await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
            State = DeviceState.Disconnected;
            return DeviceResult.Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure($"{ErrorCodePrefix}.disconnect_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult<string>> ReadPagesAsync(
        int startPage,
        int pageCount,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var validation = ValidateRead(startPage, pageCount);
            if (validation is not null)
                return validation;

            var value = await ReadPagesCoreAsync(startPage, pageCount, cancellationToken).ConfigureAwait(false);
            return DeviceResult<string>.Ok(value.TrimEnd('\0'));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult<string>.Failure($"{ErrorCodePrefix}.read_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult> WritePagesAsync(
        int startPage,
        IReadOnlyList<string> pages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var validation = ValidateWrite(startPage, pages);
            if (validation is not null)
                return validation;

            await WritePagesCoreAsync(startPage, pages, cancellationToken).ConfigureAwait(false);
            return DeviceResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DeviceResult.Failure($"{ErrorCodePrefix}.write_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        await DisconnectAsync().ConfigureAwait(false);
        await DisposeCoreAsync().ConfigureAwait(false);
        _operationGate.Dispose();
    }

    private DeviceResult<string>? ValidateRead(int startPage, int pageCount)
    {
        if (State != DeviceState.Connected)
            return DeviceResult<string>.Failure($"{ErrorCodePrefix}.not_connected", $"RFID '{Name}' 尚未连接。");
        if (startPage <= 0)
            return DeviceResult<string>.Failure($"{ErrorCodePrefix}.page_out_of_range", "起始页码必须大于 0。");
        if (pageCount <= 0)
            return DeviceResult<string>.Failure($"{ErrorCodePrefix}.page_count_out_of_range", "读取页数必须大于 0。");
        return null;
    }

    private DeviceResult? ValidateWrite(int startPage, IReadOnlyList<string> pages)
    {
        if (State != DeviceState.Connected)
            return DeviceResult.Failure($"{ErrorCodePrefix}.not_connected", $"RFID '{Name}' 尚未连接。");
        if (startPage <= 0)
            return DeviceResult.Failure($"{ErrorCodePrefix}.page_out_of_range", "起始页码必须大于 0。");
        if (pages.Count == 0)
            return DeviceResult.Failure($"{ErrorCodePrefix}.pages_empty", "至少需要写入一页数据。");

        for (var index = 0; index < pages.Count; index++)
        {
            if (pages[index] is null)
                return DeviceResult.Failure($"{ErrorCodePrefix}.page_null", $"第 {index + 1} 个写入页不能为空引用。");

            var byteCount = Encoding.UTF8.GetByteCount(pages[index]);
            if (byteCount > Options.PageSizeBytes)
            {
                return DeviceResult.Failure(
                    $"{ErrorCodePrefix}.page_too_large",
                    $"第 {index + 1} 个写入页为 {byteCount} 字节，超过 {Options.PageSizeBytes} 字节限制。");
            }
        }

        return null;
    }
}
