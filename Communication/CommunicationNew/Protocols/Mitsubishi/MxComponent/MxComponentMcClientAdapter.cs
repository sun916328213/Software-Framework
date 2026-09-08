using System.Runtime.InteropServices;
using ActSupportMsg64Lib;
using ActUtlType64Lib;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>
/// 直接调用 MX Component V5 64 位逻辑站控件的 MC adapter。
/// COM 对象按需创建，所有调用串行执行，避免同一控件实例被并发访问。
/// </summary>
internal sealed class MxComponentMcClientAdapter : IMcClientAdapter
{
    private readonly int _logicalStationNumber;
    private readonly string _password;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private ActUtlType64Class? _control;
    private ActSupportMsg64Class? _supportMessages;
    private Task? _disposeTask;
    private volatile bool _disposeStarted;

    public MxComponentMcClientAdapter(int logicalStationNumber, string? password)
    {
        _logicalStationNumber = logicalStationNumber;
        _password = password ?? string.Empty;
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
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("MX Component 直接模式仅支持 Windows。");
            }
            if (!Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException(
                    "当前引用的是 MX Component 64 位 Interop，宿主进程必须以 x64 运行。");
            }

            State = ConnectionState.Connecting;
            try
            {
                _control ??= new ActUtlType64Class();
                _control.ActLogicalStationNumber = _logicalStationNumber;
                _control.ActPassword = _password;
                ThrowForError(_control.Open(), "Open");
                State = ConnectionState.Connected;
            }
            catch (COMException ex)
            {
                State = ConnectionState.Faulted;
                ReleaseControl();
                throw new MxComponentException(ex.ErrorCode, "Open");
            }
            catch
            {
                State = ConnectionState.Faulted;
                ReleaseControl();
                throw;
            }
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
            if (State != ConnectionState.Connected || _control is null)
            {
                throw new InvalidOperationException("MX Component adapter 尚未连接。");
            }

            try
            {
                return request.IsWrite
                    ? ExecuteWrite(request)
                    : ExecuteRead(request);
            }
            catch (COMException ex)
            {
                State = ConnectionState.Faulted;
                ReleaseControl();
                throw new MxComponentException(
                    ex.ErrorCode,
                    request.IsWrite ? "Write" : "Read");
            }
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
            if (_control is null)
            {
                State = ConnectionState.Disconnected;
                return;
            }

            try
            {
                if (State == ConnectionState.Connected)
                {
                    ThrowForError(_control.Close(), "Close");
                }
                State = ConnectionState.Disconnected;
            }
            catch (COMException ex)
            {
                State = ConnectionState.Faulted;
                throw new MxComponentException(ex.ErrorCode, "Close");
            }
            finally
            {
                ReleaseControl();
            }
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
                // 先关闭入口，再排队等待当前 COM 调用，防止释放任务后方继续进入新操作。
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
            try
            {
                if (_control is not null && State == ConnectionState.Connected)
                {
                    _control.Close();
                }
            }
            catch (COMException)
            {
                // Dispose 不能恢复 COM 连接；释放 RCW 比传播关闭异常更重要。
            }
            finally
            {
                ReleaseControl();
                State = ConnectionState.Disconnected;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ThrowIfDisposing()
    {
        ObjectDisposedException.ThrowIf(_disposeStarted, this);
    }

    private McResponse ExecuteRead(McRequest request)
    {
        if (request.Address.IsBit)
        {
            return new McResponse(ReadBitBlock(request));
        }

        var words = new short[request.Quantity];
        ThrowForError(
            _control!.ReadDeviceBlock2(
                request.Address.ToString(),
                words.Length,
                out words[0]),
            "ReadDeviceBlock2");
        return new McResponse(MxComponentWordCodec.ToPayload(words));
    }

    private byte[] ReadBitBlock(McRequest request)
    {
        // MX 按字读取位设备，再将每个字按低位优先展开为统一的 MC 位响应格式。
        var window = MxComponentBitCodec.CreateReadWindow(
            request.Address.Number,
            request.Quantity);
        var words = new short[window.WordCount];
        ThrowForError(
            _control!.ReadDeviceBlock2(
                request.Address.FormatDevice(window.AlignedStart),
                words.Length,
                out words[0]),
            "ReadDeviceBlock2");
        return MxComponentBitCodec.PackReadWords(
            words,
            window.BitOffset,
            request.Quantity);
    }

    private McResponse ExecuteWrite(McRequest request)
    {
        if (request.Address.IsBit)
        {
            WriteBitBlock(request);
            return new McResponse(ReadOnlyMemory<byte>.Empty);
        }

        var words = MxComponentWordCodec.FromPayload(request.Payload.ToArray());
        ThrowForError(
            _control!.WriteDeviceBlock2(
                request.Address.ToString(),
                words.Length,
                ref words[0]),
            "WriteDeviceBlock2");
        return new McResponse(ReadOnlyMemory<byte>.Empty);
    }

    private void WriteBitBlock(McRequest request)
    {
        // 逐点写可保护请求范围之外、但位于同一设备字中的相邻位。
        var payload = request.Payload.ToArray();
        for (var index = 0; index < request.Quantity; index++)
        {
            ThrowForError(
                _control!.SetDevice2(
                    request.Address.FormatDevice(request.Address.Number + index),
                    MxComponentBitCodec.GetWriteValue(payload, index)),
                "SetDevice2");
        }
    }

    private void ThrowForError(int errorCode, string operation)
    {
        if (errorCode != 0)
        {
            throw new MxComponentException(
                errorCode,
                operation,
                TryGetVendorMessage(errorCode));
        }
    }

    private string? TryGetVendorMessage(int errorCode)
    {
        try
        {
            _supportMessages ??= new ActSupportMsg64Class();
            var message = string.Empty;
            return _supportMessages.GetErrorMessage(errorCode, out message) == 0 &&
                   !string.IsNullOrWhiteSpace(message)
                ? message
                : null;
        }
        catch (Exception)
        {
            // 错误文本属于辅助诊断，获取失败不能覆盖原始 MX 返回码。
            return null;
        }
    }

    private void ReleaseControl()
    {
        FinalReleaseComObject(_supportMessages);
        _supportMessages = null;
        FinalReleaseComObject(_control);
        _control = null;
    }

    private static void FinalReleaseComObject(object? instance)
    {
        if (OperatingSystem.IsWindows() &&
            instance is not null &&
            Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
