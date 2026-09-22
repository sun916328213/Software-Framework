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
    [RelayCommand]
    private Task ConnectToggleAsync() => Model.IsConnected
        ? RunAsync("断开相机", Workspace.DisconnectAsync)
        : RunAsync("连接相机", Workspace.ConnectAsync);

    [RelayCommand]
    private Task CaptureAsync() => RunAsync("抓图", async () =>
    {
        // 对标旧 Grab：单帧采集前先停实时，避免两条循环抢帧。
        Workspace.StopLive();
        await Workspace.CaptureAsync();
    });

    [RelayCommand]
    private void Live() => Run(() =>
    {
        Workspace.ToggleLive();
        Log($"✓ {(Model.LiveButtonText == "实时关闭" ? "已开启实时预览" : "已停止实时预览")}");
    });

    /// <summary>把测试卡上的触发配置下发到已连接的相机（未连接时什么都不做，连接时会按档案重新播种）。</summary>
    [RelayCommand]
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
}
