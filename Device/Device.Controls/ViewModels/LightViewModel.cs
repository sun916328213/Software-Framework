using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>光源控制器测试页 ViewModel：单通道/全通道亮度下发。</summary>
public partial class LightViewModel(LightWorkspace workspace)
    : DevicePageViewModelBase<LightWorkspace, LightPageModel, LightProfile>(workspace)
{
    protected override string CategoryName => "光源";

    [RelayCommand]
    private Task SetChannelAsync() => RunAsync($"设置通道 {Model.Channel} 亮度", Workspace.SetChannelAsync);

    [RelayCommand]
    private Task SetAllAsync() => RunAsync($"全通道亮度 {Model.AllIntensity}", Workspace.SetAllAsync);
}
