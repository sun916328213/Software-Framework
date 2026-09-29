using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Services;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace FlowEditor.ViewModels;

/// <summary>
/// 流程图容器 ViewModel。
/// 管理多标签页，共享工具箱、属性面板、输出日志和工具栏。
/// 所有服务依赖均通过构造器注入（IoC），文件对话框与消息框统一走 IDialogService。
/// </summary>
public partial class FlowChartContainerViewModel : ObservableObject
{
    internal const string ShortcutHelpText =
        "运行与文件\n" +
        "F5：运行全部流程\n" +
        "Ctrl+N：新建流程\n" +
        "Ctrl+S：保存当前 .flow 流程\n" +
        "Ctrl+O：打开单个 .flow 流程\n\n" +
        "画布与节点\n" +
        "鼠标滚轮：缩放画布\n" +
        "Shift+左键拖动：平移画布\n" +
        "鼠标中键拖动：平移画布\n" +
        "Ctrl+左键：添加或移除多选节点\n" +
        "Ctrl+C / Ctrl+V：复制、粘贴节点\n" +
        "Delete：删除选中节点或连线";

    /// <summary>容器中当前打开的流程标签页。</summary>
    public ObservableCollection<FlowChartViewModel> Tabs { get; } = new();

    private int _minimumTabCount;
    private int _maximumTabCount = int.MaxValue;
    private string _newTabNamePrefix = "流程图";

    private readonly IDialogService _dialogService;
    private readonly IFlowTabFactory _tabFactory;
    private readonly FlowChartContainerDocumentService _containerDocumentService;
    private readonly ToolboxService _toolboxService;

    private FlowChartViewModel? _activeTab;
    /// <summary>当前活动流程标签页。</summary>
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

    /// <summary>是否存在可操作的活动标签页。</summary>
    public bool HasActiveTab => ActiveTab != null;

    // ── 属性面板 ViewModel（经 IoC 容器解析，生命周期由容器管理） ──
    /// <summary>容器内共享的属性面板 ViewModel。</summary>
    public PropertyGridHostViewModel PropertyGridVM { get; }

    // ── 共享工具箱（委托给 ToolboxService） ──
    /// <summary>经过搜索和排除规则处理的工具箱分类。</summary>
    public ObservableCollection<ToolboxCategory> ToolboxCategories => _toolboxService.Categories;

    /// <summary>用于筛选共享工具箱的搜索文本。</summary>
    public string ToolboxSearchText
    {
        get => _toolboxService.SearchText;
        set => _toolboxService.SearchText = value;
    }

    private string? _containerFilePath;

    private string? _processName;

    /// <summary>容器所属流程名（AutoFlow / InitializeFlow ...，宿主设置）；向下传播到各标签页，用于节点日志按「流程名\任务名」路由。</summary>
    public string? ProcessName
    {
        get => _processName;
        set
        {
            if (string.Equals(_processName, value, StringComparison.Ordinal)) return;
            _processName = value;
            foreach (var tab in Tabs)
                tab.SetProcessName(value);
        }
    }

    /// <summary>创建流程图容器 ViewModel。</summary>
    /// <param name="dialogService">宿主对话框模块。</param>
    /// <param name="tabFactory">创建独立流程标签页的 factory。</param>
    /// <param name="propertyGrid">共享属性面板 ViewModel。</param>
    /// <param name="containerDocumentService">多流程文档协调模块。</param>
    /// <param name="toolboxService">共享工具箱模块。</param>
    public FlowChartContainerViewModel(
        IDialogService dialogService,
        IFlowTabFactory tabFactory,
        PropertyGridHostViewModel propertyGrid,
        FlowChartContainerDocumentService containerDocumentService,
        ToolboxService toolboxService)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _tabFactory = tabFactory ?? throw new ArgumentNullException(nameof(tabFactory));
        PropertyGridVM = propertyGrid ?? throw new ArgumentNullException(nameof(propertyGrid));
        _containerDocumentService = containerDocumentService ??
                                    throw new ArgumentNullException(nameof(containerDocumentService));
        _toolboxService = toolboxService ?? throw new ArgumentNullException(nameof(toolboxService));

        InitializeCommands();

        _ = _toolboxService.RefreshAsync();
        AddNewTab();
    }

    // ═══════════════════ 当前流程执行 ═══════════════════

    /// <summary>执行当前活动标签页中可运行的流程。</summary>
    private async Task RunCurrentAsync()
    {
        if (ActiveTab?.RunCommand.CanExecute(null) == true)
            await ActiveTab.RunCommand.ExecuteAsync(null);
    }

    // ═══════════════════ 全部运行 / 全部停止 ═══════════════════

    /// <summary>启动全部满足运行条件的标签页流程。</summary>
    private async Task RunAllAsync()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.RunCommand.CanExecute(null))
                tab.RunCommand.Execute(null);
        }
        await Task.CompletedTask;
    }

    /// <summary>请求停止全部正在运行的标签页流程。</summary>
    private void StopAll()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.StopCommand.CanExecute(null))
                tab.StopCommand.Execute(null);
        }
    }

    // ═══════════════════ 循环执行 / 停止 ═══════════════════

    /// <summary>启动全部满足条件的标签页循环运行。</summary>
    private async Task LoopRunAllAsync()
    {
        foreach (var tab in Tabs.ToList())
        {
            if (tab.LoopRunCommand.CanExecute(null))
                tab.LoopRunCommand.Execute(null);
        }
        await Task.CompletedTask;
    }

    /// <summary>请求停止全部标签页的循环运行。</summary>
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
            if (title.StartsWith(_newTabNamePrefix, StringComparison.Ordinal) &&
                title.Length > _newTabNamePrefix.Length)
            {
                if (int.TryParse(title[_newTabNamePrefix.Length..], out var num))
                    usedNumbers.Add(num);
            }
        }
        for (int i = 1; ; i++)
        {
            if (!usedNumbers.Contains(i))
                return i;
        }
    }

    /// <summary>在数量限制允许时创建并激活一个新流程标签页。</summary>
    public void AddNewTab()
    {
        if (Tabs.Count >= _maximumTabCount) return;
        var vm = _tabFactory.Create();
        vm.SetProcessName(ProcessName);
        vm.SetTabTitle($"{_newTabNamePrefix}{GetNextFlowNumber()}", markModified: false);
        vm.Model.StatusText = "就绪";
        // 监听选中变化，更新属性面板 ViewModel
        AttachPropertyGridHook(vm);
        Tabs.Add(vm);
        ActiveTab = vm;
        NewFlowCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
    }

    /// <summary>限制内部流程编辑器可创建和删除的标签页数量。</summary>
    public void ConfigureTabLimits(int minimum, int maximum, string namePrefix)
    {
        _minimumTabCount = Math.Max(0, minimum);
        _maximumTabCount = Math.Max(_minimumTabCount, maximum);
        _newTabNamePrefix = string.IsNullOrWhiteSpace(namePrefix) ? "流程图" : namePrefix;
        NewFlowCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
    }

    /// <summary>判断标签页数量是否低于当前上限。</summary>
    private bool CanAddNewTab() => Tabs.Count < _maximumTabCount;

    /// <summary>判断指定标签页是否存在且关闭后不会低于数量下限。</summary>
    private bool CanCloseTab(FlowChartViewModel? tab)
        => tab != null && Tabs.Contains(tab) && Tabs.Count > _minimumTabCount &&
           !tab.Model.IsRunning && !tab.Model.IsLoopRunning;

    /// <summary>为标签页绑定属性面板联动逻辑。</summary>
    private void AttachPropertyGridHook(FlowChartViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FlowChartViewModel.SelectedNode))
            {
                var expectedTarget = vm.SelectedNode?.Model;
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    // 连续快速点击时丢弃已过期的属性面板刷新。
                    if (ReferenceEquals(vm, ActiveTab) &&
                        ReferenceEquals(expectedTarget, vm.SelectedNode?.Model))
                        PropertyGridVM.TargetObject = expectedTarget;
                }));
            }
        };
        vm.Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FlowChartModel.IsRunning) or nameof(FlowChartModel.IsLoopRunning))
                CloseTabCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>在处理未保存确认后关闭指定标签页。</summary>
    private void CloseTab(FlowChartViewModel? tab)
    {
        if (tab == null || !CanCloseTab(tab)) return;

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
        if (tab.LoopStopCommand.CanExecute(null))
            tab.LoopStopCommand.Execute(null);
        else if (tab.StopCommand.CanExecute(null))
            tab.StopCommand.Execute(null);
        tab.ReleaseLogRoute();
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (Tabs.Count > 0)
            ActiveTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        else
            ActiveTab = null;
        NewFlowCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
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

    /// <summary>保存已关联文件的修改，并把全部标签页写入流程组文件。</summary>
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
    private async Task LoadAllAsync()
    {
        var path = _dialogService.ShowOpenFileDialog("多流程文件 (*.flows)|*.flows|所有文件 (*.*)|*.*");
        if (path == null) return;
        try
        {
            await LoadFlowGroup(path);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"加载多流程失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 从指定 .flows 文件加载流程组（非交互版，供启动预热等无对话框场景调用）。
    /// 文件不存在、内容为空均直接返回不报错；反序列化失败抛出由调用方捕获。
    /// 必须在 UI 线程调用以保证 ObservableCollection 绑定安全。
    /// </summary>
    public async Task LoadFlowGroup(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        var containerDoc = await Task.Run(() => _containerDocumentService.Load(path));
        if (containerDoc.Flows.Count == 0) return;

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

    // ═══════════════════ 工具箱 ═══════════════════

    /// <summary>从工具箱中排除指定名称的工具（用于 ToolBlock 内部编辑器排除自身）。</summary>
    public void ExcludeToolByName(string toolName)
        => _toolboxService.ExcludeToolByName(toolName);

    // ═══════════════════ 辅助 ═══════════════════

    /// <summary>显示流程编辑器快捷键说明。</summary>
    private void ShowShortcutHelp() =>
        _dialogService.ShowInfo(ShortcutHelpText, "流程图快捷键");

    /// <summary>通知容器及活动标签页重新计算全部命令状态。</summary>
    private void NotifyAllCanExecuteChanged()
    {
        if (ActiveTab != null)
            ActiveTab.NotifyCanExecuteChanged();
    }
}
