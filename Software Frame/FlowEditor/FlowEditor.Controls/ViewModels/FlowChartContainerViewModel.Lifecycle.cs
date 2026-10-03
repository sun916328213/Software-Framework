// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartContainerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Specialized、System.ComponentModel、System.Windows.Threading、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using FlowEditor.Controls.Models;

namespace FlowEditor.Controls.ViewModels;

/// <summary>维护当前标签的订阅，以及容器级命令与属性面板的状态。</summary>
public partial class FlowChartContainerViewModel
{
    private readonly HashSet<FlowChartViewModel> _observedTabs = new();

    /// <summary>根据标签集合增删和重置同步订阅，并更新容器命令。</summary>
    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Reset 没有旧项列表；根据当前集合协调订阅，也避免重复添加同一标签时重复订阅。
        var current = Tabs.ToHashSet();
        foreach (var tab in _observedTabs.Except(current).ToArray())
        {
            ObserveTab(tab, attach: false);
            _observedTabs.Remove(tab);
        }
        foreach (var tab in current.Except(_observedTabs).ToArray())
        {
            ObserveTab(tab, attach: true);
            _observedTabs.Add(tab);
        }
        if (ActiveTab != null && !current.Contains(ActiveTab))
            ActiveTab = Tabs.FirstOrDefault();
        NotifyContainerCommands();
    }

    /// <summary>建立或解除标签选择、运行模型和命令状态的订阅。</summary>
    private void ObserveTab(FlowChartViewModel tab, bool attach)
    {
        if (attach)
        {
            tab.PropertyChanged += OnTabPropertyChanged;
            tab.Model.PropertyChanged += OnTabModelPropertyChanged;
            tab.RunCommand.CanExecuteChanged += OnTabCommandChanged;
            tab.StopCommand.CanExecuteChanged += OnTabCommandChanged;
            tab.LoopRunCommand.CanExecuteChanged += OnTabCommandChanged;
            tab.LoopStopCommand.CanExecuteChanged += OnTabCommandChanged;
        }
        else
        {
            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.Model.PropertyChanged -= OnTabModelPropertyChanged;
            tab.RunCommand.CanExecuteChanged -= OnTabCommandChanged;
            tab.StopCommand.CanExecuteChanged -= OnTabCommandChanged;
            tab.LoopRunCommand.CanExecuteChanged -= OnTabCommandChanged;
            tab.LoopStopCommand.CanExecuteChanged -= OnTabCommandChanged;
        }
    }

    /// <summary>合并调度活动标签的节点选择，并丢弃过期请求。</summary>
    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FlowChartViewModel tab || e.PropertyName != nameof(FlowChartViewModel.SelectedNode))
            return;
        var expectedTarget = tab.SelectedNode?.Model;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (_observedTabs.Contains(tab) && ReferenceEquals(tab, ActiveTab) &&
                ReferenceEquals(expectedTarget, tab.SelectedNode?.Model))
                PropertyGridVM.TargetObject = expectedTarget;
        }));
    }

    /// <summary>标签运行或循环状态变化时更新关闭命令。</summary>
    private void OnTabModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlowChartModel.IsRunning) or nameof(FlowChartModel.IsLoopRunning))
            CloseTabCommand.NotifyCanExecuteChanged();
    }

    /// <summary>子流程命令状态变化时刷新容器级命令。</summary>
    private void OnTabCommandChanged(object? sender, EventArgs e) => NotifyContainerCommands();

    /// <summary>重新计算容器的执行、保存和标签管理命令状态。</summary>
    private void NotifyContainerCommands()
    {
        RunCurrentCommand.NotifyCanExecuteChanged();
        RunAllCommand.NotifyCanExecuteChanged();
        StopAllCommand.NotifyCanExecuteChanged();
        LoopRunAllCommand.NotifyCanExecuteChanged();
        LoopStopAllCommand.NotifyCanExecuteChanged();
        SaveAllCommand.NotifyCanExecuteChanged();
        SaveAllAsCommand.NotifyCanExecuteChanged();
        NewFlowCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
    }
    private Task? _shutdownTask;
    private bool _shuttingDown;
    /// <summary>Stop execution before detaching tabs and their output routes.</summary>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 ShutdownAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask DisposeAsync() => new(_shutdownTask ??= ShutdownAsync());
    /// <summary>Drain running commands while tab subscriptions and the dispatcher are available.</summary>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShutdownAsync。
    /// 正文中的关键协作调用包括 NotifyContainerCommands、LoopStopAll、StopAll、ToArray、Select；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    private async Task ShutdownAsync()
    {
        _shuttingDown = true;
        NotifyContainerCommands();
        LoopStopAll();
        StopAll();
        var tabs = Tabs.ToArray();
        var tasks = tabs.Select(WPFCustomControls.Workbench.WorkbenchLifetime.WaitForCommandsAsync).ToArray();
        try { await Task.WhenAll(tasks); }
        finally
        {
            foreach (var tab in tabs) RemoveTab(tab);
            Tabs.CollectionChanged -= OnTabsChanged;
        }
    }

}
