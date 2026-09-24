using System.Buffers;
using System.IO.Ports;
using System.Text;
using CommunicationNew.Transports;
using Device.Core.Devices;
using Device.Core.Scanners;

namespace Device.Runtime.Scanners.Newland;

/// <summary>
/// 基于连续串口字节流的新大陆 NLS-NVH220 扫码器适配器。
/// </summary>
/// <remarks>
/// 该型号为被动上报设备，不提供软件触发操作；适配器负责跨缓冲分帧和 UTF-8 解码。
/// </remarks>
internal sealed class NewlandNvh220Scanner : IScanner
{
    private readonly IByteStreamTransport _transport;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly List<byte> _receiveBuffer = [];
    private readonly byte[] _delimiter;
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private int _disposeStarted;

    /// <summary>使用生产串口传输创建扫码器。</summary>
    /// <param name="options">已经由工厂验证的扫码器配置。</param>
    public NewlandNvh220Scanner(ScannerOptions options)
        : this(options, CreateTransport(options))
    {
    }

    internal NewlandNvh220Scanner(ScannerOptions options, IByteStreamTransport transport)
    {
        Options = options;
        _transport = transport;
        _delimiter = Encoding.UTF8.GetBytes(options.Delimiter);
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    /// <inheritdoc />
    public ScannerOptions Options { get; }

    /// <inheritdoc />
    public event EventHandler<ScanReceivedEventArgs>? ScanReceived;

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
            await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
            _receiveBuffer.Clear();
            _receiveCancellation?.Dispose();
            _receiveCancellation = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_receiveCancellation.Token);
            State = DeviceState.Connected;
            return DeviceResult.Ok();
        }
        catch (OperationCanceledException)
        {
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            State = DeviceState.Disconnected;
            throw;
        }
        catch (Exception exception)
        {
            await CloseAfterFailureAsync().ConfigureAwait(false);
            State = DeviceState.Faulted;
            return DeviceResult.Failure("scanner.newland.connect_failed", exception.Message);
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

            _receiveCancellation?.Cancel();
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            _receiveBuffer.Clear();
            State = DeviceState.Disconnected;
            return DeviceResult.Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = DeviceState.Faulted;
            return DeviceResult.Failure("scanner.newland.disconnect_failed", exception.Message);
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
        _receiveCancellation?.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
        _lifecycleGate.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _transport.Input.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;
                foreach (var segment in buffer)
                    _receiveBuffer.AddRange(segment.Span.ToArray());
                _transport.Input.AdvanceTo(buffer.End);

                if (!PublishCompletedFrames())
                {
                    // 已收到结束符但帧体超过上限时，不能先发布再检查长度。
                    // 将连接标记为故障并结束接收，避免异常设备持续占用内存或传播无效数据。
                    _receiveBuffer.Clear();
                    State = DeviceState.Faulted;
                    return;
                }

                if (_receiveBuffer.Count > Options.MaximumFrameBytes)
                {
                    _receiveBuffer.Clear();
                    State = DeviceState.Faulted;
                    return;
                }

                if (result.IsCompleted)
                {
                    State = DeviceState.Faulted;
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 主动断开通过取消读取结束循环。
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            // 关闭串口流可能以 ObjectDisposedException 解除底层等待。
        }
        catch
        {
            State = DeviceState.Faulted;
        }
    }

    /// <summary>
    /// 发布缓冲区中的全部完整帧。
    /// </summary>
    /// <returns>全部完整帧均未超过长度限制时返回 <see langword="true"/>。</returns>
    private bool PublishCompletedFrames()
    {
        while (true)
        {
            var delimiterIndex = FindDelimiter();
            if (delimiterIndex < 0)
                return true;

            // delimiterIndex 同时也是当前帧体的字节数，因此应在发布事件前检查。
            if (delimiterIndex > Options.MaximumFrameBytes)
                return false;

            var frame = _receiveBuffer.GetRange(0, delimiterIndex).ToArray();
            _receiveBuffer.RemoveRange(0, delimiterIndex + _delimiter.Length);
            if (frame.Length == 0)
                continue;

            var eventArgs = new ScanReceivedEventArgs(
                Encoding.UTF8.GetString(frame),
                frame,
                DateTimeOffset.UtcNow);
            PublishSafely(eventArgs);
        }
    }

    private int FindDelimiter()
    {
        var lastStart = _receiveBuffer.Count - _delimiter.Length;
        for (var start = 0; start <= lastStart; start++)
        {
            var matches = true;
            for (var offset = 0; offset < _delimiter.Length; offset++)
            {
                if (_receiveBuffer[start + offset] == _delimiter[offset])
                    continue;

                matches = false;
                break;
            }

            if (matches)
                return start;
        }

        return -1;
    }

    private void PublishSafely(ScanReceivedEventArgs eventArgs)
    {
        var handlers = ScanReceived?.GetInvocationList();
        if (handlers is null)
            return;

        foreach (EventHandler<ScanReceivedEventArgs> handler in handlers)
        {
            try
            {
                handler(this, eventArgs);
            }
            catch
            {
                // 一个订阅者的业务异常不能终止串口接收循环或影响其他订阅者。
            }
        }
    }

    private async ValueTask CloseAfterFailureAsync()
    {
        try
        {
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // 保留最初的连接异常作为对外诊断信息。
        }
    }

    private async ValueTask WaitForReceiveLoopAsync()
    {
        if (_receiveTask is null)
            return;

        try
        {
            await _receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 断开连接时取消接收任务属于正常结束路径。
        }
        finally
        {
            _receiveTask = null;
        }
    }

    private static IByteStreamTransport CreateTransport(ScannerOptions options)
    {
        var transportOptions = new SerialTransportOptions(options.PortName.Trim(), options.BaudRate)
        {
            DataBits = options.DataBits,
            StopBits = options.StopBits == 2 ? StopBits.Two : StopBits.One,
            Parity = ParseParity(options.Parity),
            BufferSize = Math.Max(4_096, options.MaximumFrameBytes),
            TimeoutMs = options.TimeoutMilliseconds,
        };
        return new SerialByteStreamTransport(transportOptions);
    }

    private static Parity ParseParity(string parity) => parity.Trim().ToLowerInvariant() switch
    {
        "none" => Parity.None,
        "odd" => Parity.Odd,
        "even" => Parity.Even,
        "mark" => Parity.Mark,
        "space" => Parity.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, "不支持的串口校验方式。"),
    };
}
