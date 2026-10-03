// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartContainerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、CommunityToolkit.Mvvm.Input。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using CommunityToolkit.Mvvm.Input;

namespace FlowEditor.Controls.ViewModels;

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
    public AsyncRelayCommand LoadAllCommand { get; private set; } = null!;
    /// <summary>获取显示流程图快捷键说明的命令。</summary>
    public RelayCommand ShowShortcutHelpCommand { get; private set; } = null!;

    /// <summary>创建容器使用的全部命令实例。</summary>
    private void InitializeCommands()
    {
        RunCurrentCommand = new AsyncRelayCommand(RunCurrentAsync, () => !_shuttingDown && ActiveTab?.RunCommand.CanExecute(null) == true);
        RunAllCommand = new AsyncRelayCommand(RunAllAsync, () => !_shuttingDown && Tabs.Any(tab => tab.RunCommand.CanExecute(null)));
        StopAllCommand = new RelayCommand(StopAll, () => !_shuttingDown && Tabs.Any(tab => tab.StopCommand.CanExecute(null)));
        LoopRunAllCommand = new AsyncRelayCommand(LoopRunAllAsync, () => !_shuttingDown && Tabs.Any(tab => tab.LoopRunCommand.CanExecute(null)));
        LoopStopAllCommand = new RelayCommand(LoopStopAll, () => !_shuttingDown && Tabs.Any(tab => tab.LoopStopCommand.CanExecute(null)));
        NewFlowCommand = new RelayCommand(AddNewTab, CanAddNewTab);
        CloseTabCommand = new RelayCommand<FlowChartViewModel>(CloseTab, CanCloseTab);
        SaveAllCommand = new RelayCommand(SaveAll, () => !_shuttingDown && Tabs.Count > 0);
        SaveAllAsCommand = new RelayCommand(SaveAllAs, () => !_shuttingDown && Tabs.Count > 0);
        LoadAllCommand = new AsyncRelayCommand(LoadAllAsync, () => !_shuttingDown);
        ShowShortcutHelpCommand = new RelayCommand(ShowShortcutHelp, () => !_shuttingDown);
    }
}
