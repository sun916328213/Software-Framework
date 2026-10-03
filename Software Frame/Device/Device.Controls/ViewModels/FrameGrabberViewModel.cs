// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：FrameGrabberViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.Input、Device.Controls.Models、Device.Controls.Services。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>采集卡测试页 ViewModel：连续推帧渲染，无主动抓图命令。</summary>
public partial class FrameGrabberViewModel(FrameGrabberWorkspace workspace)
    : DevicePageViewModelBase<FrameGrabberWorkspace, FrameGrabberPageModel, FrameGrabberProfile>(workspace)
{
    protected override string CategoryName => "采集卡";

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResetCount。
    /// 正文中的关键协作调用包括 nameof、Run、ResetFrameCount、Log；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetCount() => Run(() =>
    {
        Workspace.ResetFrameCount();
        Log("✓ 帧计数已清零");
    });
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCommandStates。
    /// 正文中的关键协作调用包括 NotifyCommandStates、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    protected override void NotifyCommandStates()
    {
        base.NotifyCommandStates();
        ResetCountCommand.NotifyCanExecuteChanged();
    }

}
