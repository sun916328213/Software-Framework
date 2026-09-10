using CommunityToolkit.Mvvm.Input;

namespace FlowEditor.ViewModels;

/// <summary>包含多标签流程容器的命令接口及其初始化逻辑。</summary>
public partial class FlowChartContainerViewModel
{
    /// <summary>获取运行当前标签页的命令。</summary>
    public AsyncRelayCommand RunCurrentCommand { get; private set; } = null!;
    /// <summary>获取运行所有标签页的命令。</summary>
    public AsyncRelayCommand RunAllCommand { get; private set; } = null!;
    /// <summary>获取停止所有标签页的命令。</summary>
    public RelayCommand StopAllCommand { get; private set; } = null!;
    /// <summary>获取循环运行所有标签页的命令。</summary>
    public AsyncRelayCommand LoopRunAllCommand { get; private set; } = null!;
    /// <summary>获取停止所有循环运行的命令。</summary>
    public RelayCommand LoopStopAllCommand { get; private set; } = null!;
    /// <summary>获取新建流程标签页的命令。</summary>
    public RelayCommand NewFlowCommand { get; private set; } = null!;
    /// <summary>获取关闭指定标签页的命令。</summary>
    public RelayCommand<FlowChartViewModel> CloseTabCommand { get; private set; } = null!;
    /// <summary>获取保存全部流程的命令。</summary>
    public RelayCommand SaveAllCommand { get; private set; } = null!;
    /// <summary>获取全部流程另存为的命令。</summary>
    public RelayCommand SaveAllAsCommand { get; private set; } = null!;
    /// <summary>获取加载多流程文档的命令。</summary>
    public RelayCommand LoadAllCommand { get; private set; } = null!;
    /// <summary>获取显示流程图快捷键说明的命令。</summary>
    public RelayCommand ShowShortcutHelpCommand { get; private set; } = null!;

    /// <summary>创建容器使用的全部命令实例。</summary>
    private void InitializeCommands()
    {
        RunCurrentCommand = new AsyncRelayCommand(RunCurrentAsync, () => ActiveTab != null);
        RunAllCommand = new AsyncRelayCommand(RunAllAsync);
        StopAllCommand = new RelayCommand(StopAll);
        LoopRunAllCommand = new AsyncRelayCommand(LoopRunAllAsync);
        LoopStopAllCommand = new RelayCommand(LoopStopAll);
        NewFlowCommand = new RelayCommand(AddNewTab, CanAddNewTab);
        CloseTabCommand = new RelayCommand<FlowChartViewModel>(CloseTab, CanCloseTab);
        SaveAllCommand = new RelayCommand(SaveAll);
        SaveAllAsCommand = new RelayCommand(SaveAllAs);
        LoadAllCommand = new RelayCommand(LoadAll);
        ShowShortcutHelpCommand = new RelayCommand(ShowShortcutHelp);
    }
}
