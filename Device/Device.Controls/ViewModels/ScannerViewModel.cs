using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>扫码器测试页 ViewModel：被动收件箱，仅提供清空。</summary>
public partial class ScannerViewModel(ScannerWorkspace workspace)
    : DevicePageViewModelBase<ScannerWorkspace, ScannerPageModel, ScannerProfile>(workspace)
{
    protected override string CategoryName => "扫码器";

    [RelayCommand]
    private void ClearScans() => Run(() =>
    {
        Model.Scans.Clear();
        Log("✓ 已清空扫码记录");
    });
}
