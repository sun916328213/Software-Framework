// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：MxComponentMcClientAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、System.Runtime.InteropServices、ActSupportMsg64Lib、ActUtlType64Lib、Communication.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using System.Runtime.InteropServices;
using ActSupportMsg64Lib;
using ActUtlType64Lib;
using Communication.Core.Abstractions;

namespace Communication.Runtime.Protocols.Mitsubishi;

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
    private ActUtlType64? _control;
    private ActSupportMsg64? _supportMessages;
    private Task? _disposeTask;
    private volatile bool _disposeStarted;

    /// <summary>
    /// 初始化 MxComponentMcClientAdapter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public MxComponentMcClientAdapter(int logicalStationNumber, string? password)
    {
        _logicalStationNumber = logicalStationNumber;
        _password = password ?? string.Empty;
    }

    /// <inheritdoc />
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposing、ConfigureAwait、WaitAsync、IsWindows、ThrowForError；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                throw new PlatformNotSupportedException(
                    "当前 MX Component adapter 使用 64 位 Interop，仅支持 Windows x64 进程。");
            }

            State = ConnectionState.Connecting;
            try
            {
                _control ??= new ActUtlType64();
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
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、ThrowIfDisposing、ConfigureAwait、WaitAsync、ExecuteWrite；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposing、ConfigureAwait、WaitAsync、ThrowForError、Close；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 DisposeCoreAsync；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </remarks>
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

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeCoreAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Close、ReleaseControl、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposing。
    /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
    /// </summary>
    private void ThrowIfDisposing()
    {
        ObjectDisposedException.ThrowIf(_disposeStarted, this);
    }

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteRead。
    /// 正文中的关键协作调用包括 ReadBitBlock、ThrowForError、ReadDeviceBlock2、ToString、ToPayload；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadBitBlock。
    /// 正文中的关键协作调用包括 CreateReadWindow、ThrowForError、ReadDeviceBlock2、FormatDevice、PackReadWords；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteWrite。
    /// 正文中的关键协作调用包括 WriteBitBlock、FromPayload、ToArray、ThrowForError、WriteDeviceBlock2；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 执行写入操作；对应入口为 WriteBitBlock。
    /// 正文中的关键协作调用包括 ToArray、ThrowForError、SetDevice2、FormatDevice、GetWriteValue；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ThrowForError。
    /// 正文中的关键协作调用包括 TryGetVendorMessage；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetVendorMessage。
    /// 正文中的关键协作调用包括 GetErrorMessage、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private string? TryGetVendorMessage(int errorCode)
    {
        try
        {
            _supportMessages ??= new ActSupportMsg64();
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseControl。
    /// 正文中的关键协作调用包括 FinalReleaseComObject；具体条件和先后次序见方法体。
    /// </summary>
    private void ReleaseControl()
    {
        FinalReleaseComObject(_supportMessages);
        _supportMessages = null;
        FinalReleaseComObject(_control);
        _control = null;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FinalReleaseComObject。
    /// 正文中的关键协作调用包括 IsWindows、IsComObject、FinalReleaseComObject；具体条件和先后次序见方法体。
    /// </summary>
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
