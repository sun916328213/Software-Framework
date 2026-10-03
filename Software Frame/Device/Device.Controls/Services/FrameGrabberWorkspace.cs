// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：FrameGrabberWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Threading、Device.Core.Devices、Device.Core.FrameGrabbers、Device.Controls.Models、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildKey。
    /// </summary>
    protected override string BuildKey(FrameGrabberProfile profile) =>
        $"{profile.DriverId}:{profile.ConfigurationFilePath}:{profile.BoardIndex}:{profile.Connector}:{profile.PixelFormat}:{profile.BufferCount}";

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDevice。
    /// 正文中的关键协作调用包括 Create、ToOptions；具体条件和先后次序见方法体。
    /// </summary>
    protected override IDevice CreateDevice(FrameGrabberProfile profile) =>
        factory.Create(profile.ToOptions());

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResetFrameCount。
    /// 正文中的关键协作调用包括 UIInvokeSafe；具体条件和先后次序见方法体。
    /// </summary>
    public void ResetFrameCount() => DispatcherEx.UIInvokeSafe(() => Model.FrameCount = 0);

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnDeviceCreated。
    /// 正文中的关键协作调用包括 RenderFrame；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnDeviceCreated(IDevice device)
    {
        if (device is not IFrameGrabber grabber)
            return;

        _frameHandler = (_, e) => RenderFrame(e.Frame);
        grabber.FrameReceived += _frameHandler;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnDeviceReleasing。
    /// </summary>
    protected override void OnDeviceReleasing(IDevice device)
    {
        if (device is IFrameGrabber grabber && _frameHandler is not null)
            grabber.FrameReceived -= _frameHandler;
        _frameHandler = null;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConnectionChanged。
    /// </summary>
    protected override void OnConnectionChanged(bool connected)
    {
        if (connected)
            Model.FrameCount = 0;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RenderFrame。
    /// 正文中的关键协作调用包括 CompareExchange、Render、UIInvokeSafe、ShowFrame、Exchange；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
