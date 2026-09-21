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

/// <summary>标签打印页 ViewModel：模板/字段/预览/打印全流程。</summary>
public partial class LabelPrintViewModel(LabelPrintWorkspace workspace)
    : DevicePageViewModelBase<LabelPrintWorkspace, LabelPrintPageModel, LabelPrintProfile>(workspace)
{
    protected override string CategoryName => "标签打印引擎";

    [RelayCommand]
    private Task LoadPrintersAsync() => RunAsync("读取打印机列表", Workspace.LoadPrintersAsync);

    [RelayCommand]
    private Task OpenTemplateAsync() => RunAsync("打开模板", Workspace.OpenTemplateAsync);

    [RelayCommand]
    private Task ReloadFieldsAsync() => RunAsync("读取模板字段", Workspace.ReloadFieldsAsync);

    [RelayCommand]
    private Task ApplyFieldsAsync() => RunAsync("下发字段", Workspace.ApplyFieldsAsync);

    [RelayCommand]
    private Task PreviewAsync() => RunAsync("渲染预览", Workspace.RenderPreviewAsync);

    [RelayCommand]
    private Task PrintAsync() => RunAsync($"打印 {Model.Copies} 份", Workspace.PrintAsync);

    [RelayCommand]
    private Task CloseTemplateAsync() => RunAsync("关闭模板", Workspace.CloseTemplateAsync);
}
