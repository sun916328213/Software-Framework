// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DevicePageViewModelBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Device.Controls.Models、Device.Controls.Services、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
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
public abstract partial class DevicePageViewModelBase<TWorkspace, TModel, TProfile> : ObservableObject, IDisposable
    where TWorkspace : DeviceWorkspaceBase<TModel, TProfile>
    where TModel : DevicePageModelBase<TProfile>, new()
    where TProfile : class, IDeviceProfile, new()
{
    /// <summary>
    /// 初始化 DevicePageViewModelBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    protected DevicePageViewModelBase(TWorkspace workspace)
    {
        Workspace = workspace;
        Model.PropertyChanged += OnModelChanged;
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
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => RunAsync($"连接{CategoryName}", Workspace.ConnectAsync);

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 nameof、RunAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private Task DisconnectAsync() => RunAsync($"断开{CategoryName}", Workspace.DisconnectAsync);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearOutput。
    /// </summary>
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
    protected void Log(string msg) => DispatcherEx.UIInvokeSafe(() =>
    {
        if (!IsClosed) OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText;
    });

    /// <summary>
    /// 运行当前处理流程；对应入口为 RunAsync。
    /// 正文中的关键协作调用包括 CanEdit、StartNew、action、Stop、Log；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    protected async Task RunAsync(string title, Func<Task> action)
    {
        if (!CanEdit()) return;
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

    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 CanEdit、action、Log；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    protected void Run(Action action)
    {
        if (!CanEdit()) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log($"✗ {ex.Message}");
        }
    }
    public bool IsClosed { get; private set; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanEdit。
    /// </summary>
    protected bool CanEdit() => !IsClosed && !Workspace.IsClosed && !IsBusy;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanOperate。
    /// 正文中的关键协作调用包括 CanEdit；具体条件和先后次序见方法体。
    /// </summary>
    protected bool CanOperate() => CanEdit() && Model.IsConnected;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanConnect。
    /// 正文中的关键协作调用包括 CanEdit；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanConnect() => CanEdit() && !Model.IsConnected;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanDisconnect。
    /// 正文中的关键协作调用包括 CanOperate；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanDisconnect() => CanOperate();
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsBusyChanged。
    /// 正文中的关键协作调用包括 NotifyCommandStates；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 nameof、NotifyCommandStates；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Model.IsConnected)) NotifyCommandStates();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCommandStates。
    /// 正文中的关键协作调用包括 NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    protected virtual void NotifyCommandStates()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 NotifyCommandStates；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (IsClosed) return;
        IsClosed = true;
        Model.PropertyChanged -= OnModelChanged;
        NotifyCommandStates();
    }

}
