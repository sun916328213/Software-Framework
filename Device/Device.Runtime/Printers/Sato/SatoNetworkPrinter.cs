using System.Net.Sockets;
using Device.Core.Devices;
using Device.Core.Printers;

namespace Device.Runtime.Printers.Sato;

/// <summary>
/// 通过 TCP 发送 SBPL 数据并使用 ENQ 查询状态的 SATO 网络打印机适配器。
/// </summary>
/// <remarks>
/// 打印机网络端口需要启用 Status4 ENQ 状态返回协议。
/// 驱动同时兼容端口 9100 启用或禁用“旧版状态”时附加的长度前缀。
/// 原始打印内容由调用方生成，本适配器不修改其中的 ESC、STX 或 ETX 控制字节。
/// </remarks>
internal sealed class SatoNetworkPrinter : IPrinter
{
    private static readonly byte[] StatusRequest = [0x05]; // ENQ
    private const int MaximumStatusResponseBytes = 256;

    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _disposeStarted;

    /// <summary>使用已经由工厂验证的配置创建尚未连接的 SATO 打印机。</summary>
    /// <param name="options">打印机配置快照。</param>
    public SatoNetworkPrinter(PrinterOptions options)
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
    public async ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        TcpClient? client = null;
        try
        {
            ThrowIfDisposed();
            if (State == DeviceState.Connected)
                return DeviceResult.Ok();

            CloseConnection();
            State = DeviceState.Connecting;
            client = new TcpClient { NoDelay = true };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Options.MaximumReadTimeoutMilliseconds);
            try
            {
                await client
                    .ConnectAsync(Options.IpAddress, Options.Port, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"连接 SATO 打印机超时（{Options.MaximumReadTimeoutMilliseconds} 毫秒）。");
            }

            _client = client;
            _stream = client.GetStream();
            client = null;
            State = DeviceState.Connected;
            return DeviceResult.Ok();
        }
        catch (ObjectDisposedException)
        {
            client?.Dispose();
            throw;
        }
        catch (OperationCanceledException)
        {
            client?.Dispose();
            CloseConnection();
            State = DeviceState.Disconnected;
            throw;
        }
        catch (Exception exception)
        {
            client?.Dispose();
            CloseConnection();
            State = DeviceState.Faulted;
            return DeviceResult.Failure("printer.sato.connect_failed", exception.Message);
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
            ThrowIfDisposed();
            CloseConnection();
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
            return DeviceResult.Failure("printer.sato.disconnect_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult<PrinterStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var requestSent = false;
        try
        {
            ThrowIfDisposed();
            if (State != DeviceState.Connected || _stream is null)
            {
                return DeviceResult<PrinterStatus>.Failure(
                    "printer.sato.not_connected",
                    $"打印机 '{Name}' 尚未连接。");
            }

            // 从开始写入 ENQ 起就可能已有部分字节进入套接字；后续取消时不能继续复用连接。
            requestSent = true;
            await _stream.WriteAsync(StatusRequest, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Options.MaximumReadTimeoutMilliseconds);
            byte[] response;
            try
            {
                response = await ReadStatusResponseAsync(_stream, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 响应可能在超时后到达并残留在连接中，必须重连以恢复清晰的帧边界。
                State = DeviceState.Faulted;
                return DeviceResult<PrinterStatus>.Failure(
                    "printer.sato.status_timeout",
                    $"SATO 打印机在 {Options.MaximumReadTimeoutMilliseconds} 毫秒内未返回状态。");
            }

            return DeviceResult<PrinterStatus>.Ok(ParseStatus(response));
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            if (requestSent)
                State = DeviceState.Faulted;
            throw;
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return DeviceResult<PrinterStatus>.Failure("printer.sato.status_failed", exception.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult> PrintRawAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (data.IsEmpty)
                return DeviceResult.Failure("printer.sato.print_data_empty", "打印数据不能为空。");

            if (State != DeviceState.Connected || _stream is null)
            {
                return DeviceResult.Failure(
                    "printer.sato.not_connected",
                    $"打印机 '{Name}' 尚未连接。");
            }

            await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return DeviceResult.Ok();
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // 取消可能发生在部分 SBPL 字节已经写入之后，连接不能安全复用。
            State = DeviceState.Faulted;
            throw;
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure("printer.sato.print_failed", exception.Message);
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

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseConnection();
            State = DeviceState.Disconnected;
        }
        catch
        {
            // 释放失败表示对象尚未成功结束生命周期，允许调用方再次尝试释放。
            State = DeviceState.Faulted;
            Volatile.Write(ref _disposeStarted, 0);
            throw;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>解析 SATO ENQ 返回的 Status4 状态帧。</summary>
    /// <param name="response">
    /// 包含可选长度前缀、ENQ、STX、25 字节 Status4 负载和 ETX 的原始响应。
    /// </param>
    /// <returns>与厂商状态类型解耦的状态快照。</returns>
    internal static PrinterStatus ParseStatus(ReadOnlySpan<byte> response)
    {
        var start = response.IndexOf((byte)0x02);
        if (start < 0)
            throw new FormatException("SATO 状态响应缺少 STX。");

        var relativeEnd = response[(start + 1)..].IndexOf((byte)0x03);
        if (relativeEnd < 0)
            throw new FormatException("SATO 状态响应缺少 ETX。");

        var end = start + 1 + relativeEnd;
        var payload = response[(start + 1)..end];
        if (payload.Length != 25)
        {
            throw new FormatException(
                $"SATO Status4 负载应为 25 字节，实际为 {payload.Length} 字节。");
        }

        // Status4：2 字节状态 ID + 1 字节状态码 + 6 字节剩余张数 + 16 字节作业名。
        // 在线且接收缓冲区未接近满载时才能继续接收新的打印任务。
        var status = (char)payload[2];
        var ready = status is 'A' or 'B' or 'G' or 'H' or 'M' or 'N' or 'S' or 'T';
        var paused = status is '0' or '1' or '2' or '3' or '4' or 'E' or 'K' or 'Q' or 'W';
        var mediaNearEnd = status is '1' or '3' or 'B' or 'D' or 'H' or 'J' or 'N' or 'P' or 'T' or 'V';
        var receiveBufferNearFull = status is '2' or '3' or 'C' or 'D' or 'I' or 'J' or 'O' or 'P' or 'U' or 'V';

        // 状态字段的类型大多是 bool，使用命名参数可避免后续扩展时因位置变化而映射错位。
        return new PrinterStatus(
            ReadyToPrint: ready,
            Paused: paused,
            HeadOpen: status == 'b',
            PaperOut: status == 'c',
            RibbonOut: status == 'd',
            ReceiveBufferFull: status == 'a',
            MediaNearEnd: mediaNearEnd,
            ReceiveBufferNearFull: receiveBufferNearFull,
            AdditionalFaultCode: GetAdditionalFaultCode(status),
            LabelsRemainingInBatch: ParseRemainingLabelCount(payload[3..9]));
    }

    /// <summary>解析 Status4 中固定六位的剩余标签数量。</summary>
    /// <param name="bytes">六个 ASCII 数字。</param>
    /// <returns>当前批次尚未打印的标签数量。</returns>
    private static int ParseRemainingLabelCount(ReadOnlySpan<byte> bytes)
    {
        var value = 0;
        foreach (var current in bytes)
        {
            if (current is < (byte)'0' or > (byte)'9')
                throw new FormatException("SATO Status4 剩余标签数量不是有效的六位数字。");

            value = checked((value * 10) + current - (byte)'0');
        }

        return value;
    }

    /// <summary>把没有独立公共布尔字段的 Status4 错误转换为稳定字符串故障码。</summary>
    /// <param name="status">Status4 单字节状态码。</param>
    /// <returns>附加故障码；状态已由公共字段表达或不属于错误时返回 <see langword="null"/>。</returns>
    private static string? GetAdditionalFaultCode(char status) => status switch
    {
        'e' => PrinterFaultCodes.MediaError,
        'f' => PrinterFaultCodes.SensorError,
        'g' => PrinterFaultCodes.HeadError,
        'h' => PrinterFaultCodes.CoverOpen,
        'i' => PrinterFaultCodes.StorageError,
        'j' => PrinterFaultCodes.CutterError,
        'k' => PrinterFaultCodes.OtherError,
        'l' => PrinterFaultCodes.CutterSensorError,
        'm' => PrinterFaultCodes.StackerFull,
        'o' => PrinterFaultCodes.RfidTagError,
        'p' => PrinterFaultCodes.RfidProtectError,
        'q' => PrinterFaultCodes.BatteryError,
        >= 'a' and <= 'z' => PrinterFaultCodes.UnknownError,
        _ => null,
    };

    private static async ValueTask<byte[]> ReadStatusResponseAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var result = new List<byte>();
        var buffer = new byte[64];
        while (result.Count < MaximumStatusResponseBytes)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
                throw new IOException("SATO 打印机在状态帧结束前关闭了连接。");

            for (var index = 0; index < count; index++)
            {
                if (result.Count == MaximumStatusResponseBytes)
                {
                    throw new InvalidDataException(
                        $"SATO 状态响应超过 {MaximumStatusResponseBytes} 字节，未找到 ETX。");
                }

                result.Add(buffer[index]);
                if (buffer[index] == 0x03)
                    return result.ToArray();
            }
        }

        throw new InvalidDataException(
            $"SATO 状态响应超过 {MaximumStatusResponseBytes} 字节，未找到 ETX。");
    }

    private void CloseConnection()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    /// <summary>对象开始释放后拒绝新的设备操作。</summary>
    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
}
