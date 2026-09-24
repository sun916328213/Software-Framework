using System.Runtime.InteropServices;
using Device.Core.FrameGrabbers;
using Matrox.MatroxImagingLibrary;

namespace Device.Runtime.FrameGrabbers.Matrox;

/// <summary>
/// 基于 Matrox Imaging Library 的 Radient eV-CL 连续采集适配器。
/// </summary>
internal sealed class MatroxRadientFrameGrabber : FrameGrabberBase
{
    private readonly MIL_ID[] _grabBuffers;
    private MIL_ID _application = MIL.M_NULL;
    private MIL_ID _system = MIL.M_NULL;
    private MIL_ID _digitizer = MIL.M_NULL;
    private MIL_INT _width;
    private MIL_INT _height;
    private int _allocatedBufferCount;
    private long _sequence;
    private GCHandle _callbackHandle;
    private MIL_DIG_HOOK_FUNCTION_PTR? _processingCallback;
    private bool _processingStarted;

    /// <summary>使用生产 Matrox SDK 创建未连接的采集卡。</summary>
    /// <param name="options">已经由工厂验证的 Mono8 配置。</param>
    public MatroxRadientFrameGrabber(FrameGrabberOptions options)
        : base(options)
    {
        _grabBuffers = new MIL_ID[options.BufferCount];
    }

    /// <inheritdoc />
    protected override string ErrorCodePrefix => "frame-grabber.matrox.radient-evcl";

    /// <inheritdoc />
    protected override void ConnectCore()
    {
        MIL.MappAlloc("M_DEFAULT", MIL.M_DEFAULT, ref _application);
        MIL.MsysAlloc(
            _application,
            MIL.M_SYSTEM_RADIENTEVCL,
            Options.BoardIndex,
            MIL.M_DEFAULT,
            ref _system);
        MIL.MdigAlloc(
            _system,
            MIL.M_DEFAULT,
            Options.ConfigurationFilePath,
            MIL.M_DEFAULT,
            ref _digitizer);

        _width = MIL.MdigInquire(_digitizer, MIL.M_SIZE_X, MIL.M_NULL);
        _height = MIL.MdigInquire(_digitizer, MIL.M_SIZE_Y, MIL.M_NULL);
        if (_width <= 0 || _height <= 0)
            throw new InvalidOperationException("Matrox SDK 返回了无效的图像尺寸。");

        MIL.MdigControl(_digitizer, MIL.M_PROCESS_GRAB_MONITOR, MIL.M_DISABLE);
        MIL.MappControl(MIL.M_DEFAULT, MIL.M_ERROR, MIL.M_PRINT_DISABLE);
        try
        {
            while (_allocatedBufferCount < _grabBuffers.Length)
            {
                var bufferIndex = _allocatedBufferCount;
                MIL.MbufAlloc2d(
                    _system,
                    _width,
                    _height,
                    8 + MIL.M_UNSIGNED,
                    MIL.M_IMAGE + MIL.M_GRAB + MIL.M_PROC,
                    ref _grabBuffers[bufferIndex]);
                if (_grabBuffers[bufferIndex] == MIL.M_NULL)
                    break;

                // 分配成功后立即登记所有权；即使随后清零失败，连接失败清理也能释放该缓冲区。
                _allocatedBufferCount++;
                MIL.MbufClear(_grabBuffers[bufferIndex], 0);
            }
        }
        finally
        {
            MIL.MappControl(MIL.M_DEFAULT, MIL.M_ERROR, MIL.M_PRINT_ENABLE);
        }

        if (_allocatedBufferCount == 0)
            throw new InvalidOperationException("Matrox SDK 未能分配任何采集缓冲区。");

        _sequence = 0;
        _callbackHandle = GCHandle.Alloc(this);
        _processingCallback = ProcessingCallback;
        MIL.MdigProcess(
            _digitizer,
            _grabBuffers,
            _allocatedBufferCount,
            MIL.M_START,
            MIL.M_DEFAULT,
            _processingCallback,
            GCHandle.ToIntPtr(_callbackHandle));
        _processingStarted = true;
    }

    /// <inheritdoc />
    protected override void DisconnectCore()
    {
        if (_processingStarted && _digitizer != MIL.M_NULL && _processingCallback is not null)
        {
            MIL.MdigProcess(
                _digitizer,
                _grabBuffers,
                _allocatedBufferCount,
                MIL.M_STOP,
                MIL.M_DEFAULT,
                _processingCallback,
                _callbackHandle.IsAllocated ? GCHandle.ToIntPtr(_callbackHandle) : IntPtr.Zero);
            _processingStarted = false;
        }

        if (_callbackHandle.IsAllocated)
            _callbackHandle.Free();
        _processingCallback = null;

        while (_allocatedBufferCount > 0)
        {
            var index = --_allocatedBufferCount;
            if (_grabBuffers[index] != MIL.M_NULL)
            {
                MIL.MbufFree(_grabBuffers[index]);
                _grabBuffers[index] = MIL.M_NULL;
            }
        }

        if (_digitizer != MIL.M_NULL)
        {
            MIL.MdigFree(_digitizer);
            _digitizer = MIL.M_NULL;
        }
        if (_system != MIL.M_NULL)
        {
            MIL.MsysFree(_system);
            _system = MIL.M_NULL;
        }
        if (_application != MIL.M_NULL)
        {
            MIL.MappFree(_application);
            _application = MIL.M_NULL;
        }
    }

    private MIL_INT ProcessingCallback(MIL_INT hookType, MIL_ID hookId, IntPtr hookDataPointer)
    {
        try
        {
            MIL_ID modifiedBuffer = MIL.M_NULL;
            MIL.MdigGetHookInfo(
                hookId,
                MIL.M_MODIFIED_BUFFER + MIL.M_BUFFER_ID,
                ref modifiedBuffer);
            if (modifiedBuffer == MIL.M_NULL)
                return 0;

            var byteCount = checked((int)(_width * _height));
            var data = new byte[byteCount];
            MIL.MbufGet(modifiedBuffer, data);
            PublishFrame(new FrameGrabberFrame(
                (int)_width,
                (int)_height,
                (int)_width,
                Options.PixelFormat,
                data,
                Interlocked.Increment(ref _sequence),
                DateTimeOffset.UtcNow));
        }
        catch
        {
            // 异常不能穿越非托管回调；下一帧仍允许继续采集。
        }

        return 0;
    }
}
