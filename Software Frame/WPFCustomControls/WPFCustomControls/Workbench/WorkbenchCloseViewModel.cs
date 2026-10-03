// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：WorkbenchCloseViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WPFCustomControls.Workbench;

/// <summary>Reports close progress and collects an explicit decision after a timeout.</summary>
/// <remarks>
/// WorkbenchCloseViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class WorkbenchCloseViewModel : ObservableObject
{
    private string _status = "正在停止任务并释放资源…";
    private bool _isTimedOut;
    private TaskCompletionSource<bool>? _decision;

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsTimedOut
    {
        get => _isTimedOut;
        private set
        {
            if (!SetProperty(ref _isTimedOut, value)) return;
            ContinueWaitingCommand.NotifyCanExecuteChanged();
            CloseWithoutWaitingCommand.NotifyCanExecuteChanged();
        }
    }
    public IRelayCommand ContinueWaitingCommand { get; }
    public IRelayCommand CloseWithoutWaitingCommand { get; }

    /// <summary>
    /// 初始化 WorkbenchCloseViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public WorkbenchCloseViewModel()
    {
        ContinueWaitingCommand = new RelayCommand(() => _decision?.TrySetResult(true), () => IsTimedOut);
        CloseWithoutWaitingCommand = new RelayCommand(() => _decision?.TrySetResult(false), () => IsTimedOut);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportProgress。
    /// </summary>
    internal void ReportProgress(int current, int total)
    {
        if (!IsTimedOut) Status = $"正在释放资源（{current}/{total}）…";
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 AskAfterTimeoutAsync。
    /// </summary>
    internal Task<bool> AskAfterTimeoutAsync()
    {
        _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Status = "资源清理超时。可以继续等待，或仍然关闭窗口。\n仍然关闭时，设备或后台任务的清理可能尚未完成。";
        IsTimedOut = true;
        return _decision.Task;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResumeWaiting。
    /// </summary>
    internal void ResumeWaiting()
    {
        IsTimedOut = false;
        Status = "继续等待资源清理…";
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Finish。
    /// </summary>
    internal void Finish()
    {
        IsTimedOut = false;
        _decision?.TrySetResult(true);
        Status = "资源清理完成。";
    }
}
