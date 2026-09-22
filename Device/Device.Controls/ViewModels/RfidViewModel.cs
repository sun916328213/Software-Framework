using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>RFID 测试页 ViewModel：按页读/写。</summary>
public partial class RfidViewModel(RfidWorkspace workspace)
    : DevicePageViewModelBase<RfidWorkspace, RfidPageModel, RfidProfile>(workspace)
{
    protected override string CategoryName => "RFID";

    [RelayCommand]
    private Task ReadAsync() => RunAsync($"读取 {Model.PageCount} 页", Workspace.ReadAsync);

    [RelayCommand]
    private Task WriteAsync() => RunAsync("写入卡片", Workspace.WriteAsync);
}
