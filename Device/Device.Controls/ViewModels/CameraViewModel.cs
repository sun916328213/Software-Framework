using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;
using Extension.UI.Controls;

namespace Device.Controls.ViewModels;

/// <summary>
/// 相机测试页 ViewModel：只暴露 <see cref="CameraPageModel"/> + 装配命令。
/// 状态在 Model，流程在 Workspace；运行输出/忙指示/计时是纯展示层反馈，留在此处。
/// </summary>
public partial class CameraViewModel : ObservableObject
{
    private readonly CameraWorkspace _workspace;

    /// <summary>页状态（Workspace 持有）。</summary>
    public CameraPageModel Model => _workspace.Model;

    public CameraViewModel(CameraWorkspace workspace)
    {
        _workspace = workspace;
    }

    /// <summary>供 XAML 绑定的增加回调（命名 + 选中；保存由控件保存按钮完成）。</summary>
    public Action ProfileAddedAction => OnProfilesAddedExternally;

    /// <summary>供 XAML 绑定的删除回调（保底至少留一个）。</summary>
    public Action ProfileDeletedAction => OnProfilesDeletedExternally;

    /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
    public Action ProfilesSavedAction => OnProfilesSavedExternally;

    /// <summary>供 XAML 绑定的选中回调（对标 CamManagerView 的 SelectedCallback → SetCurCam）。</summary>
    public Action SelectCallbackAction => OnProfileSelectedExternally;

    // ---------- 运行反馈（展示层） ----------
    [ObservableProperty]
    private string outputText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string lastElapsed = "-";

    // ---------- 命令（全部转调 Workspace，计时/日志由 Run 系列包装） ----------
    [RelayCommand]
    private Task ConnectAsync() => RunAsync("连接相机", _workspace.ConnectAsync);

    [RelayCommand]
    private Task DisconnectAsync() => RunAsync("断开相机", _workspace.DisconnectAsync);

    [RelayCommand]
    private Task CaptureAsync() => RunAsync("抓图", async () =>
    {
        // 对标旧 Grab：单帧采集前先停实时，避免两条循环抢帧。
        _workspace.StopLive();
        await _workspace.CaptureAsync();
    });

    [RelayCommand]
    private void Live() => Run(() =>
    {
        _workspace.ToggleLive();
        Log($"✓ {(Model.LiveButtonText == "实时关闭" ? "已开启实时预览" : "已停止实时预览")}");
    });

    [RelayCommand]
    private void ShowCross() => Run(() =>
    {
        Model.IsShowCross = !Model.IsShowCross;
        Model.ShowCrossButtonText = Model.IsShowCross ? "关闭中心十字线" : "显示中心十字线";
    });

    [RelayCommand]
    private Task ScanAsync() => RunAsync("扫描在线相机", async () =>
    {
        var warning = await _workspace.ScanDevicesAsync();
        var count = Model.Devices.Count;
        if (warning is null)
            Log($"✓ 发现 {count} 台在线相机");
        else
            Log($"⚠ {warning}（已发现 {count} 台）");
    });

    [RelayCommand]
    private void AddFromDevice() => Run(() =>
    {
        _workspace.AddProfileFromDevice();
        Log($"✓ 已按选中设备创建档案：{Model.SelectedProfile?.Name}（点保存落盘）");
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

    [RelayCommand]
    private void ClearOutput() => OutputText = string.Empty;

    /// <summary>
    /// ConfigManagerView 增加按钮回调：控件已用 Activator 加了空行，此处命名 + 选中。
    /// </summary>
    public void OnProfilesAddedExternally() => Run(() =>
    {
        var profile = Model.DataCollection.LastOrDefault(p => string.IsNullOrWhiteSpace(p.Name))
            ?? new CameraProfile { Name = $"相机{Model.DataCollection.Count + 1}" };
        if (!Model.DataCollection.Contains(profile))
            Model.DataCollection.Add(profile);
        else
            profile.Name = $"相机{Model.DataCollection.Count}";
        Model.SelectedProfile = profile;
        Log($"✓ 已添加相机档案：{profile.Name}（点保存落盘）");
    });

    /// <summary>ConfigManagerView 删除按钮回调：控件已移除选中行，此处保底至少留一个。</summary>
    public void OnProfilesDeletedExternally() => Run(() =>
    {
        if (Model.DataCollection.Count == 0)
        {
            var fallback = new CameraProfile { Name = "相机01" };
            Model.DataCollection.Add(fallback);
            Model.SelectedProfile = fallback;
        }
        else if (Model.SelectedProfile is null || !Model.DataCollection.Contains(Model.SelectedProfile))
        {
            Model.SelectedProfile = Model.DataCollection[^1];
        }
    });

    /// <summary>ConfigManagerView 保存按钮回调：控件已写文件，此处记日志。</summary>
    public void OnProfilesSavedExternally() => Run(() =>
        Log($"✓ 已保存 {Model.DataCollection.Count} 个相机档案 → {Model.FullPath}"));

    /// <summary>ConfigManagerView 选中行回调：停实时并让代理属性随新行重新通知。</summary>
    public void OnProfileSelectedExternally() => Run(() =>
    {
        _workspace.SetCurCam();
        Log($"✓ 当前相机：{Model.SelectedProfile?.Name ?? "<无>"}");
    });

    // ---------- 展示层：日志输出/计时/忙指示 ----------
    private void Log(string msg) => DispatcherEx.UIInvokeSafe(
        () => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText);

    private async Task RunAsync(string title, Func<Task> action)
    {
        IsBusy = true;
        LastElapsed = "执行中…";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await action();
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            Log($"✓ {title} ({LastElapsed})");
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            Log($"✗ {title}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log($"✗ {ex.Message}");
        }
    }
}
