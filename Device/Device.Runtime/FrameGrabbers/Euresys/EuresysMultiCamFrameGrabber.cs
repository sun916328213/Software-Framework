using System.Runtime.InteropServices;
using Device.Core.FrameGrabbers;

namespace Device.Runtime.FrameGrabbers.Euresys;

/// <summary>
/// 基于 Euresys MultiCam 的连续采集适配器。
/// </summary>
internal sealed class EuresysMultiCamFrameGrabber : FrameGrabberBase
{
    private uint _channel;
    private bool _driverOpen;
    private long _sequence;
    private MultiCamNative.Callback? _callback;

    /// <summary>使用生产 MultiCam 驱动创建未连接的采集卡。</summary>
    /// <param name="options">已经由工厂验证的采集卡配置。</param>
    public EuresysMultiCamFrameGrabber(FrameGrabberOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override string ErrorCodePrefix => "frame-grabber.euresys.multicam";

    /// <inheritdoc />
    protected override void ConnectCore()
    {
        MultiCamNative.OpenDriver();
        _driverOpen = true;
        _channel = MultiCamNative.CreateChannel();

        MultiCamNative.Set(_channel, "DriverIndex", Options.BoardIndex);
        MultiCamNative.Set(_channel, "Connector", Options.Connector.Trim());
        MultiCamNative.Set(_channel, "CamFile", Options.ConfigurationFilePath);
        MultiCamNative.Set(_channel, "SeqLength_Fr", -1);

        _sequence = 0;
        _callback = OnMultiCamSignal;
        MultiCamNative.RegisterCallback(_channel, _callback, _channel);
        MultiCamNative.Set(
            _channel,
            MultiCamNative.SignalEnable + MultiCamNative.SurfaceProcessingSignal,
            "ON");
        MultiCamNative.Set(
            _channel,
            MultiCamNative.SignalEnable + MultiCamNative.AcquisitionFailureSignal,
            "OFF");
        MultiCamNative.Set(_channel, "ChannelState", "READY");
        MultiCamNative.Set(_channel, "ChannelState", "ACTIVE");
        MultiCamNative.Set(_channel, "ForceTrig", "TRIG");
    }

    /// <inheritdoc />
    protected override void DisconnectCore()
    {
        Exception? firstError = null;
        if (_channel != 0)
        {
            TryCleanup(() => MultiCamNative.Set(_channel, "ChannelState", "IDLE"), ref firstError);
            var channel = _channel;
            _channel = 0;
            TryCleanup(() => MultiCamNative.Delete(channel), ref firstError);
        }

        _callback = null;
        if (_driverOpen)
        {
            _driverOpen = false;
            TryCleanup(MultiCamNative.CloseDriver, ref firstError);
        }

        if (firstError is not null)
            throw firstError;
    }

    private void OnMultiCamSignal(ref MultiCamNative.SignalInfo signalInfo)
    {
        if (signalInfo.Signal != MultiCamNative.SurfaceProcessingSignal)
            return;

        try
        {
            var surface = signalInfo.SignalInfoValue;
            var width = MultiCamNative.GetInt32(_channel, "ImageSizeX");
            var height = MultiCamNative.GetInt32(_channel, "ImageSizeY");
            var nativePitch = MultiCamNative.GetInt32(_channel, "BufferPitch");
            var address = MultiCamNative.GetPointer(surface, "SurfaceAddr");
            var stride = Math.Abs(nativePitch);
            if (width <= 0 || height <= 0 || stride <= 0 || address == IntPtr.Zero)
                return;

            var byteCount = checked(stride * height);
            var data = new byte[byteCount];
            Marshal.Copy(address, data, 0, byteCount);
            PublishFrame(new FrameGrabberFrame(
                width,
                height,
                stride,
                Options.PixelFormat,
                data,
                Interlocked.Increment(ref _sequence),
                DateTimeOffset.UtcNow));
        }
        catch
        {
            // 异常不能穿越非托管回调；下一帧仍允许继续采集。
        }
    }

    private static void TryCleanup(Action cleanup, ref Exception? firstError)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }
    }
}
