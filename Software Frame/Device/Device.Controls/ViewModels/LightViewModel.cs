// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LightViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.Input、Device.Controls.Models、Device.Controls.Services。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>光源控制器测试页 ViewModel：单通道/全通道亮度下发。</summary>
public partial class LightViewModel(LightWorkspace workspace)
    : DevicePageViewModelBase<LightWorkspace, LightPageModel, LightProfile>(workspace)
{
    protected override string CategoryName => "光源";

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetChannelAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task SetChannelAsync() => RunAsync($"设置通道 {Model.Channel} 亮度", Workspace.SetChannelAsync);

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetAllAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task SetAllAsync() => RunAsync($"全通道亮度 {Model.AllIntensity}", Workspace.SetAllAsync);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCommandStates。
    /// 正文中的关键协作调用包括 NotifyCommandStates、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    protected override void NotifyCommandStates()
    {
        base.NotifyCommandStates();
        SetChannelCommand.NotifyCanExecuteChanged();
        SetAllCommand.NotifyCanExecuteChanged();
    }

}
