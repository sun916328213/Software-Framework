// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：EuresysMultiCamFrameGrabber。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.FrameGrabbers、System.Runtime.InteropServices、Device.Core.FrameGrabbers。导入不代表所有类型都会在运行时加载。
using Device.Runtime.FrameGrabbers;
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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectCore。
    /// 正文中的关键协作调用包括 OpenDriver、CreateChannel、Set、Trim、RegisterCallback；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectCore。
    /// 正文中的关键协作调用包括 TryCleanup、Set、Delete；具体条件和先后次序见方法体。
    /// </remarks>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnMultiCamSignal。
    /// 正文中的关键协作调用包括 GetInt32、GetPointer、Abs、Copy、PublishFrame；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryCleanup。
    /// 正文中的关键协作调用包括 cleanup；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
