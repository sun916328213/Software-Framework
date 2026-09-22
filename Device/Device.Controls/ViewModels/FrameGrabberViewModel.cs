using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>采集卡测试页 ViewModel：连续推帧渲染，无主动抓图命令。</summary>
public partial class FrameGrabberViewModel(FrameGrabberWorkspace workspace)
    : DevicePageViewModelBase<FrameGrabberWorkspace, FrameGrabberPageModel, FrameGrabberProfile>(workspace)
{
    protected override string CategoryName => "采集卡";

    [RelayCommand]
    private void ResetCount() => Run(() =>
    {
        Workspace.ResetFrameCount();
        Log("✓ 帧计数已清零");
    });
}
