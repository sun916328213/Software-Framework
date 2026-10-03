// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：RfidViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.Input、Device.Controls.Models、Device.Controls.Services。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>RFID 测试页 ViewModel：按页读/写。</summary>
public partial class RfidViewModel(RfidWorkspace workspace)
    : DevicePageViewModelBase<RfidWorkspace, RfidPageModel, RfidProfile>(workspace)
{
    protected override string CategoryName => "RFID";

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task ReadAsync() => RunAsync($"读取 {Model.PageCount} 页", Workspace.ReadAsync);

    /// <summary>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task WriteAsync() => RunAsync("写入卡片", Workspace.WriteAsync);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCommandStates。
    /// 正文中的关键协作调用包括 NotifyCommandStates、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    protected override void NotifyCommandStates()
    {
        base.NotifyCommandStates();
        ReadCommand.NotifyCanExecuteChanged();
        WriteCommand.NotifyCanExecuteChanged();
    }

}
