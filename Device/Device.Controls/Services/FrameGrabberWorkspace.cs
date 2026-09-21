using System.Threading;
using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Device.Controls.Models;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 采集卡测试页流程：连接后由驱动持续推帧，本页订阅 <see cref="IFrameGrabber.FrameReceived"/>
/// 渲染回填预览并计数；不主动抓图。
/// </summary>
public sealed class FrameGrabberWorkspace(IFrameGrabberFactory factory)
    : DeviceWorkspaceBase<FrameGrabberPageModel, FrameGrabberProfile>(factory.AvailableDrivers)
{
    private EventHandler<FrameReceivedEventArgs>? _frameHandler;
    private int _renderBusy;

    protected override string CategoryName => "采集卡";

    protected override string FallbackProfileName => "采集卡01";

    protected override string BuildKey(FrameGrabberProfile profile) =>
        $"{profile.DriverId}:{profile.ConfigurationFilePath}:{profile.BoardIndex}:{profile.Connector}:{profile.PixelFormat}:{profile.BufferCount}";

    protected override IDevice CreateDevice(FrameGrabberProfile profile) =>
        factory.Create(profile.ToOptions());

    public void ResetFrameCount() => DispatcherEx.UIInvokeSafe(() => Model.FrameCount = 0);

    protected override void OnDeviceCreated(IDevice device)
    {
        if (device is not IFrameGrabber grabber)
            return;

        _frameHandler = (_, e) => RenderFrame(e.Frame);
        grabber.FrameReceived += _frameHandler;
    }

    protected override void OnDeviceReleasing(IDevice device)
    {
        if (device is IFrameGrabber grabber && _frameHandler is not null)
            grabber.FrameReceived -= _frameHandler;
        _frameHandler = null;
    }

    protected override void OnConnectionChanged(bool connected)
    {
        if (connected)
            Model.FrameCount = 0;
    }

    private void RenderFrame(FrameGrabberFrame frame)
    {
        // 上一帧还在渲染就丢弃本帧，避免连续推帧压垮 UI。
        if (Interlocked.CompareExchange(ref _renderBusy, 1, 0) != 0)
            return;

        try
        {
            var image = FrameGrabberFrameRenderer.Render(frame);
            DispatcherEx.UIInvokeSafe(() =>
                Model.ShowFrame(image, frame.Width, frame.Height, frame.PixelFormat, frame.Sequence));
        }
        catch
        {
            // 渲染失败的帧静默丢弃（错误格式在页头 FrameInfo 保持上一帧说明）。
        }
        finally
        {
            Interlocked.Exchange(ref _renderBusy, 0);
        }
    }
}
