using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Models;
using Device.Controls.Services;
using Extension.UI.Controls;

namespace Device.Controls.ViewModels;

/// <summary>
/// 设备测试页 ViewModel 基类（对标 CameraViewModel 的通用部分）：
/// 暴露 Model + 连接/断开命令 + ConfigManagerView 四类回调 + 运行输出反馈；
/// 流程一律转调 Workspace，各类别专属命令在派生类追加。
/// </summary>
public abstract partial class DevicePageViewModelBase<TWorkspace, TModel, TProfile> : ObservableObject
    where TWorkspace : DeviceWorkspaceBase<TModel, TProfile>
    where TModel : DevicePageModelBase<TProfile>, new()
    where TProfile : class, IDeviceProfile, new()
{
    protected DevicePageViewModelBase(TWorkspace workspace)
    {
        Workspace = workspace;
    }

    protected TWorkspace Workspace { get; }

    /// <summary>页状态（Workspace 持有）。</summary>
    public TModel Model => Workspace.Model;

    /// <summary>类别中文名（与 Workspace 的 CategoryName 保持一致）。</summary>
    protected abstract string CategoryName { get; }

    // ---------- 运行反馈（展示层） ----------
    [ObservableProperty]
    private string outputText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string lastElapsed = "-";

    // ---------- 通用命令 ----------
    [RelayCommand]
    private Task ConnectAsync() => RunAsync($"连接{CategoryName}", Workspace.ConnectAsync);

    [RelayCommand]
    private Task DisconnectAsync() => RunAsync($"断开{CategoryName}", Workspace.DisconnectAsync);

    [RelayCommand]
    private void ClearOutput() => OutputText = string.Empty;

    // ---------- ConfigManagerView 回调（XAML 绑定 Action 属性） ----------
    public Action ProfileAddedAction => OnProfilesAddedExternally;

    public Action ProfileDeletedAction => OnProfilesDeletedExternally;

    public Action ProfilesSavedAction => OnProfilesSavedExternally;

    public Action SelectCallbackAction => OnProfileSelectedExternally;

    /// <summary>ConfigManagerView 增加按钮回调：控件已加空行，此处命名 + 选中。</summary>
    public virtual void OnProfilesAddedExternally() => Run(() =>
    {
        Workspace.CompleteAddedProfile();
        Log($"✓ 已添加{CategoryName}档案：{Model.SelectedProfile?.Name}（点保存落盘）");
    });

    /// <summary>ConfigManagerView 删除按钮回调：控件已移除选中行，此处保底至少留一个。</summary>
    public virtual void OnProfilesDeletedExternally() => Run(Workspace.FixAfterDelete);

    /// <summary>ConfigManagerView 保存按钮回调：控件已写文件，此处记日志。</summary>
    public virtual void OnProfilesSavedExternally() => Run(() =>
        Log($"✓ 已保存 {Model.DataCollection.Count} 个{CategoryName}档案 → {Model.FullPath}"));

    /// <summary>ConfigManagerView 选中行回调。</summary>
    public virtual void OnProfileSelectedExternally() => Run(() =>
    {
        Workspace.SetCurDevice();
        Log($"✓ 当前{CategoryName}：{Model.SelectedProfile?.Name ?? "<无>"}");
    });

    // ---------- 展示层：日志输出/计时/忙指示 ----------
    protected void Log(string msg) => DispatcherEx.UIInvokeSafe(
        () => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText);

    protected async Task RunAsync(string title, Func<Task> action)
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

    protected void Run(Action action)
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
