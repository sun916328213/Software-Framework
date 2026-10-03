// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelPrintViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.Input、Device.Controls.Models、Device.Controls.Services。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>标签打印页 ViewModel：模板/字段/预览/打印全流程。</summary>
public partial class LabelPrintViewModel(LabelPrintWorkspace workspace)
    : DevicePageViewModelBase<LabelPrintWorkspace, LabelPrintPageModel, LabelPrintProfile>(workspace)
{
    protected override string CategoryName => "标签打印引擎";

    /// <summary>
    /// 执行加载操作；对应入口为 LoadPrintersAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task LoadPrintersAsync() => RunAsync("读取打印机列表", Workspace.LoadPrintersAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 OpenTemplateAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task OpenTemplateAsync() => RunAsync("打开模板", Workspace.OpenTemplateAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReloadFieldsAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task ReloadFieldsAsync() => RunAsync("读取模板字段", Workspace.ReloadFieldsAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplyFieldsAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task ApplyFieldsAsync() => RunAsync("下发字段", Workspace.ApplyFieldsAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PreviewAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task PreviewAsync() => RunAsync("渲染预览", Workspace.RenderPreviewAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task PrintAsync() => RunAsync($"打印 {Model.Copies} 份", Workspace.PrintAsync);

    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseTemplateAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task CloseTemplateAsync() => RunAsync("关闭模板", Workspace.CloseTemplateAsync);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCommandStates。
    /// 正文中的关键协作调用包括 NotifyCommandStates、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    protected override void NotifyCommandStates()
    {
        base.NotifyCommandStates();
        LoadPrintersCommand.NotifyCanExecuteChanged();
        OpenTemplateCommand.NotifyCanExecuteChanged();
        ReloadFieldsCommand.NotifyCanExecuteChanged();
        ApplyFieldsCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
        CloseTemplateCommand.NotifyCanExecuteChanged();
    }

}
