using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

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
