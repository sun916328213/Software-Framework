using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>打印机测试页 ViewModel：状态查询 + 原始指令直发。</summary>
public partial class PrinterViewModel(PrinterWorkspace workspace)
    : DevicePageViewModelBase<PrinterWorkspace, PrinterPageModel, PrinterProfile>(workspace)
{
    protected override string CategoryName => "打印机";

    [RelayCommand]
    private Task QueryStatusAsync() => RunAsync("查询状态", Workspace.QueryStatusAsync);

    [RelayCommand]
    private Task PrintRawAsync() => RunAsync("发送原始指令", Workspace.PrintRawAsync);
}
