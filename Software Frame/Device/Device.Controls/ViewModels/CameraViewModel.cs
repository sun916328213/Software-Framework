// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.Input、Device.Controls.Models、Device.Controls.Services。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>
/// 相机测试页 ViewModel：通用部分（连接/断开、档案回调、运行输出/计时）在基类，
/// 此处只留相机专属命令（连接切换合一、抓图、实时、下发触发）。
/// </summary>
public partial class CameraViewModel(CameraWorkspace workspace)
    : DevicePageViewModelBase<CameraWorkspace, CameraPageModel, CameraProfile>(workspace)
{
    protected override string CategoryName => "相机";

    // ---------- 命令（全部转调 Workspace，计时/日志由 Run 系列包装） ----------
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task ConnectToggleAsync() => Model.IsConnected
        ? RunAsync("断开相机", Workspace.DisconnectAsync)
        : RunAsync("连接相机", Workspace.ConnectAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CaptureAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync、StopLive、CaptureAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task CaptureAsync() => RunAsync("抓图", async () =>
    {
        // 对标旧 Grab：单帧采集前先停实时，避免两条循环抢帧。
        Workspace.StopLive();
        await Workspace.CaptureAsync();
    });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Live。
    /// 正文中的关键协作调用包括 nameof、Run、ToggleLive、Log；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void Live() => Run(() =>
    {
        Workspace.ToggleLive();
        Log($"✓ {(Model.LiveButtonText == "实时关闭" ? "已开启实时预览" : "已停止实时预览")}");
    });

    /// <summary>把测试卡上的触发配置下发到已连接的相机（未连接时什么都不做，连接时会按档案重新播种）。</summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task ApplyTriggerAsync() => Model.IsConnected
        ? RunAsync("下发触发配置", async () =>
        {
            await Workspace.ApplyTriggerAsync();
            Log($"✓ 帧触发 {Model.RuntimeFrameTriggerMode}/{Model.RuntimeFrameTriggerSource}"
                + (Model.IsLineTriggerEnabled
                    ? $"，行触发 {Model.RuntimeLineTriggerMode}/{Model.RuntimeLineTriggerSource}"
                    : "，行触发不适用（面阵）"));
        })
        : Task.CompletedTask;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCommandStates。
    /// 正文中的关键协作调用包括 NotifyCommandStates、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    protected override void NotifyCommandStates()
    {
        base.NotifyCommandStates();
        ConnectToggleCommand.NotifyCanExecuteChanged();
        CaptureCommand.NotifyCanExecuteChanged();
        LiveCommand.NotifyCanExecuteChanged();
        ApplyTriggerCommand.NotifyCanExecuteChanged();
    }

}
