using System.Diagnostics;
using System.IO.Pipes;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>
/// 通过本机命名管道调用 .NET Framework MX Component 桥接进程的 MC adapter。
/// 每个实例独占一个桥接进程和请求队列，避免 COM 同步调用阻塞主进程线程。
/// </summary>
public sealed class MxComponentBridgeClientAdapter : IMcClientAdapter
{
    private const int ConnectCommand = 1;
    private const int DisconnectCommand = 2;
    private const int ExecuteCommand = 3;
    private readonly MxComponentBridgeOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private NamedPipeClientStream? _pipe;
    private Process? _process;
    private Task? _disposeTask;
    private volatile bool _disposeStarted;

    public MxComponentBridgeClientAdapter(MxComponentBridgeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ExecutablePath);
        if (!Path.IsPathFullyQualified(options.ExecutablePath))
        {
            throw new ArgumentException("MX 桥接程序必须使用绝对路径。", nameof(options));
        }
        if (options.StartupTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "启动超时必须大于零。");
        }
        if (options.OperationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "操作超时必须大于零。");
        }
        if (options.LogicalStationNumber is < 0 or > 1023)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.LogicalStationNumber,
                "MX Component 逻辑站号必须在 0 到 1023 之间。");
        }
    }

    /// <inheritdoc />
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <inheritdoc />
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposing();
            if (State == ConnectionState.Connected)
            {
                return;
            }
            if (!File.Exists(_options.ExecutablePath))
            {
                throw new FileNotFoundException("找不到 MX Component 桥接程序。", _options.ExecutablePath);
            }

            State = ConnectionState.Connecting;
            var pipeName = "CommunicationNew.Mx." + Guid.NewGuid().ToString("N");
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                Arguments = $"--pipe {pipeName} --station {_options.LogicalStationNumber}",
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("无法启动 MX Component 桥接进程。");
            _pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.StartupTimeout);
            await _pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var response = await SendCommandAsync(CreateConnectCommand(_options.Password), timeout.Token)
                .ConfigureAwait(false);
            ThrowForError(response.ErrorCode, "Open", response.Payload);
            State = ConnectionState.Connected;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            AbortBridge();
            State = ConnectionState.Faulted;
            throw new TimeoutException(
                $"MX Component 桥接进程未在 {_options.StartupTimeout} 内完成启动。",
                ex);
        }
        catch
        {
            AbortBridge();
            State = ConnectionState.Faulted;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<McResponse> ExecuteAsync(
        McRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposing();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposing();
            if (State != ConnectionState.Connected)
            {
                throw new InvalidOperationException("MX Component adapter 尚未连接。");
            }

            using var timeout = CreateOperationTimeout(cancellationToken);
            var response = await SendCommandAsync(CreateExecuteCommand(request), timeout.Token)
                .ConfigureAwait(false);
            ThrowForError(
                response.ErrorCode,
                request.IsWrite ? "Write" : "Read",
                response.Payload);
            return new McResponse(response.Payload);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // 超时后必须终止桥接代际，防止不可取消的 COM 迟到响应污染下一请求。
            AbortBridge();
            State = ConnectionState.Faulted;
            throw new TimeoutException(
                $"MX Component 操作超过 {_options.OperationTimeout}。",
                ex);
        }
        catch (OperationCanceledException)
        {
            // COM 调用无法中途取消；终止桥接代际，避免迟到响应被后续请求误读。
            AbortBridge();
            State = ConnectionState.Faulted;
            throw;
        }
        catch (IOException)
        {
            AbortBridge();
            State = ConnectionState.Faulted;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposing();
            if (_pipe is null)
            {
                State = ConnectionState.Disconnected;
                return;
            }

            using var timeout = CreateOperationTimeout(cancellationToken);
            var response = await SendCommandAsync(
                    CreateSimpleCommand(DisconnectCommand),
                    timeout.Token)
                .ConfigureAwait(false);
            ThrowForError(response.ErrorCode, "Close", response.Payload);
            AbortBridge();
            State = ConnectionState.Disconnected;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            AbortBridge();
            State = ConnectionState.Faulted;
            throw new TimeoutException(
                $"MX Component 断开超过 {_options.OperationTimeout}。",
                ex);
        }
        catch
        {
            // 显式断开失败后也必须终止这一桥接代际，不能遗留未知状态的 COM 子进程。
            AbortBridge();
            State = ConnectionState.Faulted;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            if (_disposeTask is null)
            {
                _disposeStarted = true;
                _disposeTask = DisposeCoreAsync();
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_pipe is not null)
            {
                try
                {
                    // 桥接关闭不得无限等待不可取消的 COM；超时后由 finally 强制终止进程。
                    using var timeout = new CancellationTokenSource(_options.OperationTimeout);
                    var response = await SendCommandAsync(
                            CreateSimpleCommand(DisconnectCommand),
                            timeout.Token)
                        .ConfigureAwait(false);
                    ThrowForError(response.ErrorCode, "Close", response.Payload);
                }
                catch (Exception)
                {
                    // Dispose 是最终清理路径，回收桥接进程比传播关闭失败更重要。
                }
            }
        }
        finally
        {
            AbortBridge();
            State = ConnectionState.Disconnected;
            _gate.Release();
        }
    }

    private void ThrowIfDisposing()
    {
        ObjectDisposedException.ThrowIf(_disposeStarted, this);
    }

    private async Task<BridgeResponse> SendCommandAsync(
        byte[] command,
        CancellationToken cancellationToken)
    {
        var pipe = _pipe ?? throw new InvalidOperationException("MX 桥接管道尚未建立。");
        await pipe.WriteAsync(command, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        var header = new byte[8];
        await ReadExactlyAsync(pipe, header, cancellationToken).ConfigureAwait(false);
        var errorCode = BitConverter.ToInt32(header, 0);
        var payloadLength = BitConverter.ToInt32(header, 4);
        if (payloadLength < 0 || payloadLength > 64 * 1024 * 1024)
        {
            throw new InvalidDataException($"MX 桥接返回了无效负载长度 {payloadLength}。");
        }
        var payload = new byte[payloadLength];
        await ReadExactlyAsync(pipe, payload, cancellationToken).ConfigureAwait(false);
        return new BridgeResponse(errorCode, payload);
    }

    private CancellationTokenSource CreateOperationTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.OperationTimeout);
        return timeout;
    }

    private static byte[] CreateSimpleCommand(int command)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(command);
        return stream.ToArray();
    }

    private static byte[] CreateConnectCommand(string password)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(ConnectCommand);
        writer.Write(password ?? string.Empty);
        return stream.ToArray();
    }

    private static byte[] CreateExecuteCommand(McRequest request)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(ExecuteCommand);
        writer.Write(request.IsWrite);
        writer.Write(request.Address.Prefix);
        writer.Write(request.Address.Number);
        writer.Write(request.Address.IsBit);
        writer.Write((int)request.Quantity);
        writer.Write(request.Payload.Length);
        writer.Write(request.Payload.Span);
        return stream.ToArray();
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                    buffer.AsMemory(offset),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("MX Component 桥接进程提前关闭了管道。");
            }
            offset += read;
        }
    }

    private void AbortBridge()
    {
        _pipe?.Dispose();
        _pipe = null;
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
        }
        _process?.Dispose();
        _process = null;
    }

    private static void ThrowForError(
        int errorCode,
        string operation,
        ReadOnlySpan<byte> payload)
    {
        if (errorCode != 0)
        {
            var message = payload.IsEmpty
                ? null
                : System.Text.Encoding.UTF8.GetString(payload);
            throw new MxComponentException(errorCode, operation, message);
        }
    }

    private sealed record BridgeResponse(int ErrorCode, byte[] Payload);
}
