using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Interfaces;
using FlowEditor.Services;
using Prism.Ioc;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace FlowEditor.ViewModels;

/// <summary>
/// 流程图容器 ViewModel。
/// 管理多标签页，共享工具箱、属性面板、输出日志和工具栏。
/// 所有服务依赖均通过构造器注入（IoC），文件对话框与消息框统一走 IDialogService。
/// </summary>
public partial class FlowChartContainerViewModel : ObservableObject
{
    public ObservableCollection<FlowChartViewModel> Tabs { get; } = new();

    private readonly IDialogService _dialogService;
    private readonly IContainerProvider _containerProvider;
    private readonly FlowChartContainerDocumentService _containerDocumentService;
    private readonly ToolboxService _toolboxService;

    private FlowChartViewModel? _activeTab;
    public FlowChartViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(HasActiveTab));
                RunCurrentCommand.NotifyCanExecuteChanged();
                NotifyAllCanExecuteChanged();
            }
        }
    }

    public bool HasActiveTab => ActiveTab != null;

    // ── 属性面板 ViewModel（经 IoC 容器解析，生命周期由容器管理） ──
    public PropertyGridHostViewModel PropertyGridVM { get; }

    // ── 容器级命令 ──
    public AsyncRelayCommand RunCurrentCommand { get; }
    public AsyncRelayCommand RunAllCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public AsyncRelayCommand LoopRunAllCommand { get; }
    public RelayCommand LoopStopAllCommand { get; }
    public RelayCommand NewFlowCommand { get; }
    public RelayCommand<FlowChartViewModel> CloseTabCommand { get; }
    public RelayCommand SaveAllCommand { get; }
    public RelayCommand SaveAllAsCommand { get; }
    public RelayCommand LoadAllCommand { get; }

    // ── 共享工具箱（委托给 ToolboxService） ──
    public ObservableCollection<ToolboxCategory> ToolboxCategories => _toolboxService.Categories;

    public string ToolboxSearchText
    {
        get => _toolboxService.SearchText;
        set => _toolboxService.SearchText = value;
    }

    private string? _containerFilePath;

    /// <summary>容器所属流程名（AutoFlow / InitializeFlow ...，宿主设置）；向下传播到各标签页，用于节点日志按「流程名\任务名」路由。</summary>
    public string? ProcessName { get; set; }

    public FlowChartContainerViewModel(
        IDialogService dialogService,
        IContainerProvider containerProvider,
        FlowChartContainerDocumentService containerDocumentService,
        ToolboxService toolboxService)
    {
        _dialogService = dialogService;
        _containerProvider = containerProvider;
        _containerDocumentService = containerDocumentService;
        _toolboxService = toolboxService;

        PropertyGridVM = containerProvider.Resolve<PropertyGridHostViewModel>();

        RunCurrentCommand = new AsyncRelayCommand(RunCurrentAsync, () => ActiveTab != null);
        RunAllCommand = new AsyncRelayCommand(RunAllAsync);
        StopAllCommand = new RelayCommand(StopAll);
        LoopRunAllCommand = new AsyncRelayCommand(LoopRunAllAsync);
        LoopStopAllCommand = new RelayCommand(LoopStopAll);
        NewFlowCommand = new RelayCommand(AddNewTab);
        CloseTabCommand = new RelayCommand<FlowChartViewModel>(CloseTab);
        SaveAllCommand = new RelayCommand(SaveAll);
        SaveAllAsCommand = new RelayCommand(SaveAllAs);
        LoadAllCommand = new RelayCommand(LoadAll);

        _ = _toolboxService.RefreshAsync();
        AddNewTab();
    }

    // ═══════════════════ 当前流程执行 ═══════════════════

    private async Task RunCurrentAsync()
    {
        if (ActiveTab?.RunCommand.CanExecute(null) == true)
            await ActiveTab.RunCommand.ExecuteAsync(null);
    }

    // ═══════════════════ 全部运行 / 全部停止 ═══════════════════

    private async Task RunAllAsync()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.RunCommand.CanExecute(null))
                tab.RunCommand.Execute(null);
        }
        await Task.CompletedTask;
    }

    private void StopAll()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.StopCommand.CanExecute(null))
                tab.StopCommand.Execute(null);
        }
    }

    // ═══════════════════ 循环执行 / 停止 ═══════════════════

    private async Task LoopRunAllAsync()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.LoopRunCommand.CanExecute(null))
                tab.LoopRunCommand.Execute(null);
        }
        await Task.CompletedTask;
    }

    private void LoopStopAll()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.LoopStopCommand.CanExecute(null))
                tab.LoopStopCommand.Execute(null);
        }
    }

    // ═══════════════════ 标签页管理 ═══════════════════

    /// <summary>获取下一个可用的流程图编号（复用已删除的编号）。</summary>
    private int GetNextFlowNumber()
    {
        var usedNumbers = new HashSet<int>();
        foreach (var tab in Tabs)
        {
            var title = tab.Model.TabTitle;
            if (title.StartsWith("流程图") && title.Length > 3)
            {
                if (int.TryParse(title[3..], out var num))
                    usedNumbers.Add(num);
            }
        }
        for (int i = 1; ; i++)
        {
            if (!usedNumbers.Contains(i))
                return i;
        }
    }

    public void AddNewTab()
    {
        var vm = _containerProvider.Resolve<FlowChartViewModel>();
        vm.SetProcessName(ProcessName);
        vm.Model.TabTitle = $"流程图{GetNextFlowNumber()}";
        vm.Model.StatusText = "就绪";
        // 监听选中变化，更新属性面板 ViewModel
        AttachPropertyGridHook(vm);
        Tabs.Add(vm);
        ActiveTab = vm;
    }

    /// <summary>为标签页绑定属性面板联动逻辑。</summary>
    private void AttachPropertyGridHook(FlowChartViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FlowChartViewModel.SelectedNode))
                PropertyGridVM.TargetObject = ActiveTab?.SelectedNode?.Model;
        };
    }

    private void CloseTab(FlowChartViewModel? tab)
    {
        if (tab == null) return;

        if (tab.Model.IsModified)
        {
            var result = _dialogService.ShowConfirm(
                $"流程图 \"{tab.Model.TabTitle}\" 有未保存的修改，要保存吗？", "关闭流程");
            if (result == null) return;
            if (result == true)
                tab.SaveCommand.Execute(null);
        }
        else
        {
            var confirmed = _dialogService.ConfirmYesNo(
                $"确定要删除流程图 \"{tab.Model.TabTitle}\" 吗？", "删除流程");
            if (!confirmed) return;
        }

        RemoveTab(tab);
    }

    /// <summary>直接移除标签页，不弹确认框（用于打开多流程文件等场景）。</summary>
    private void RemoveTab(FlowChartViewModel? tab)
    {
        if (tab == null) return;
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (Tabs.Count > 0)
            ActiveTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        else
            ActiveTab = null;
    }

    /// <summary>用单个流程替换当前所有标签页（用于自动流程等默认流程自动加载）。</summary>
    public void ReplaceWithFlow(FlowChartViewModel vm)
    {
        vm.SetProcessName(ProcessName);
        foreach (var tab in Tabs.ToList())
            RemoveTab(tab);
        AttachPropertyGridHook(vm);
        Tabs.Add(vm);
        ActiveTab = vm;
    }

    // ═══════════════════ 多流程持久化 ═══════════════════

    /// <summary>保存所有流程到一个文件。</summary>
    private void SaveAll()
    {
        if (!string.IsNullOrEmpty(_containerFilePath))
        {
            SaveAllInternal(_containerFilePath);
            return;
        }
        SaveAllAs();
    }

    /// <summary>将所有流程另存为。</summary>
    private void SaveAllAs()
    {
        var path = _dialogService.ShowSaveFileDialog(
            "多流程文件 (*.flows)|*.flows|所有文件 (*.*)|*.*",
            "流程图组.flows",
            ".flows");
        if (path != null)
        {
            SaveAllInternal(path);
            _containerFilePath = path;
        }
    }

    private void SaveAllInternal(string path)
    {
        try
        {
            // 先保存单个标签页（确保文件路径更新）
            foreach (var tab in Tabs)
            {
                if (tab.Model.IsModified && !string.IsNullOrEmpty(tab.Model.CurrentFilePath))
                    tab.SaveCommand.Execute(null);
            }

            var containerDoc = _containerDocumentService.BuildContainerDocument(Tabs);
            _containerDocumentService.Save(path, containerDoc);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"保存多流程失败：{ex.Message}");
        }
    }

    /// <summary>从文件加载多个流程。</summary>
    private void LoadAll()
    {
        var path = _dialogService.ShowOpenFileDialog("多流程文件 (*.flows)|*.flows|所有文件 (*.*)|*.*");
        if (path == null) return;

        try
        {
            var containerDoc = _containerDocumentService.Load(path);
            if (containerDoc.Flows.Count == 0)
            {
                _dialogService.ShowInfo("多流程文件内容为空");
                return;
            }

            // 关闭所有现有标签页（不弹确认框）
            foreach (var tab in Tabs.ToList())
                RemoveTab(tab);

            // 加载每个流程
            foreach (var flowDoc in containerDoc.Flows)
            {
                var vm = _containerDocumentService.CreateTabFromFlow(flowDoc);
                vm.SetProcessName(ProcessName);
                AttachPropertyGridHook(vm);
                Tabs.Add(vm);
            }

            _containerFilePath = path;
            ActiveTab = Tabs.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"加载多流程失败：{ex.Message}");
        }
    }

    // ═══════════════════ 工具箱 ═══════════════════

    /// <summary>从工具箱中排除指定名称的工具（用于 ToolBlock 内部编辑器排除自身）。</summary>
    public void ExcludeToolByName(string toolName)
        => _toolboxService.ExcludeToolByName(toolName);

    // ═══════════════════ 辅助 ═══════════════════

    private void NotifyAllCanExecuteChanged()
    {
        if (ActiveTab != null)
            ActiveTab.NotifyCanExecuteChanged();
    }
}
