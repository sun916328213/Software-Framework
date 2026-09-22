using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;

namespace Device.Controls.ViewModels;

/// <summary>
/// 相机测试页 ViewModel：通用部分（连接/断开、档案回调、运行输出/计时）在基类，
/// 此处只留相机专属命令（连接切换合一、抓图、实时、保存图像）。
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

    [RelayCommand]
    private Task SaveImageAsync() => RunAsync("保存图像", () =>
    {
        var preview = Model.Preview as BitmapSource ?? throw new InvalidOperationException("请先抓图");
        return Task.Run(() =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 图像 (*.png)|*.png",
                FileName = $"camera-frame-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            };
            if (dialog.ShowDialog() != true)
                return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(preview));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            Log($"✓ 已保存 {stream.Length} 字节 → {dialog.FileName}");
        });
    });
}
