using System.Windows;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Models;

namespace FlowEditor.ViewModels;

/// <summary>包含单个流程画布的命令接口及其初始化逻辑。</summary>
public partial class FlowChartViewModel
{
    /// <summary>获取运行当前流程的命令。</summary>
    public AsyncRelayCommand RunCommand { get; private set; } = null!;
    /// <summary>获取停止当前流程的命令。</summary>
    public RelayCommand StopCommand { get; private set; } = null!;
    /// <summary>获取循环运行当前流程的命令。</summary>
    public AsyncRelayCommand LoopRunCommand { get; private set; } = null!;
    /// <summary>获取停止循环运行的命令。</summary>
    public RelayCommand LoopStopCommand { get; private set; } = null!;
    /// <summary>获取保存当前流程的命令。</summary>
    public RelayCommand SaveCommand { get; private set; } = null!;
    /// <summary>获取当前流程另存为的命令。</summary>
    public RelayCommand SaveAsCommand { get; private set; } = null!;
    /// <summary>获取加载流程文件的命令。</summary>
    public RelayCommand LoadCommand { get; private set; } = null!;
    /// <summary>获取清空当前流程的命令。</summary>
    public RelayCommand ClearAllCommand { get; private set; } = null!;
    /// <summary>获取重置当前流程的命令。</summary>
    public RelayCommand NewFlowCommand { get; private set; } = null!;
    /// <summary>获取一键清空当前标签页输出栏的命令。</summary>
    public RelayCommand ClearOutputCommand { get; private set; } = null!;
    /// <summary>获取放大画布的命令。</summary>
    public RelayCommand ZoomInCommand { get; private set; } = null!;
    /// <summary>获取缩小画布的命令。</summary>
    public RelayCommand ZoomOutCommand { get; private set; } = null!;
    /// <summary>获取重置画布缩放的命令。</summary>
    public RelayCommand ZoomResetCommand { get; private set; } = null!;
    /// <summary>获取画布自适应视口的命令。</summary>
    public RelayCommand ZoomToFitCommand { get; private set; } = null!;
    /// <summary>获取删除当前选中内容的命令。</summary>
    public RelayCommand DeleteSelectedCommand { get; private set; } = null!;
    /// <summary>获取撤销命令。</summary>
    public RelayCommand UndoCommand { get; private set; } = null!;
    /// <summary>获取重做命令。</summary>
    public RelayCommand RedoCommand { get; private set; } = null!;
    /// <summary>获取调试单步继续命令。</summary>
    public AsyncRelayCommand StepContinueCommand { get; private set; } = null!;
    /// <summary>获取从指定节点恢复或跳转执行的命令。</summary>
    public AsyncRelayCommand<NodeViewModel> ContinueFromNodeCommand { get; private set; } = null!;
    /// <summary>获取将指定视觉元素导出为 SVG 的兼容命令。</summary>
    public RelayCommand<FrameworkElement?> ExportSvgCommand { get; private set; } = null!;
    /// <summary>获取复制选中节点的命令。</summary>
    public RelayCommand CopyCommand { get; private set; } = null!;
    /// <summary>获取粘贴节点的命令。</summary>
    public RelayCommand PasteCommand { get; private set; } = null!;
    /// <summary>获取删除指定连线的命令。</summary>
    public RelayCommand<ConnectionModel?> DeleteConnectionCommand { get; private set; } = null!;
    /// <summary>获取新增输入端口的命令。</summary>
    public RelayCommand<NodeViewModel> AddInputPortCommand { get; private set; } = null!;
    /// <summary>获取新增输出端口的命令。</summary>
    public RelayCommand<NodeViewModel> AddOutputPortCommand { get; private set; } = null!;
    /// <summary>获取复制指定节点的命令。</summary>
    public RelayCommand<NodeViewModel> CopyNodeCommand { get; private set; } = null!;
    /// <summary>获取删除指定节点的命令。</summary>
    public RelayCommand<NodeViewModel> DeleteNodeCommand { get; private set; } = null!;
    /// <summary>获取把节点权限设为正常的命令。</summary>
    public RelayCommand<NodeViewModel> SetNormalPermissionCommand { get; private set; } = null!;
    /// <summary>获取把节点权限设为只读的命令。</summary>
    public RelayCommand<NodeViewModel> SetReadOnlyPermissionCommand { get; private set; } = null!;
    /// <summary>获取把节点权限设为禁用的命令。</summary>
    public RelayCommand<NodeViewModel> SetDisabledPermissionCommand { get; private set; } = null!;
    /// <summary>获取将节点移入不执行容器的命令。</summary>
    public RelayCommand<NodeViewModel> MoveToInactiveContainerCommand { get; private set; } = null!;
    /// <summary>获取将节点移回执行画布的命令。</summary>
    public RelayCommand<NodeViewModel> RestoreFromInactiveContainerCommand { get; private set; } = null!;

    /// <summary>创建当前流程使用的全部命令实例。</summary>
    private void InitializeCommands()
    {
        RunCommand = new AsyncRelayCommand(RunAsync, CanRun);
        StopCommand = new RelayCommand(Stop, CanStop);
        LoopRunCommand = new AsyncRelayCommand(LoopRunAsync, CanLoopRun);
        LoopStopCommand = new RelayCommand(LoopStop, CanLoopStop);
        SaveCommand = new RelayCommand(Save);
        SaveAsCommand = new RelayCommand(SaveAs);
        LoadCommand = new RelayCommand(Load);
        ClearAllCommand = new RelayCommand(ClearAll);
        NewFlowCommand = new RelayCommand(NewFlow);
        ClearOutputCommand = new RelayCommand(Service.ClearOutput);
        ZoomInCommand = new RelayCommand(Service.ZoomIn);
        ZoomOutCommand = new RelayCommand(Service.ZoomOut);
        ZoomResetCommand = new RelayCommand(Service.ZoomReset);
        ZoomToFitCommand = new RelayCommand(Service.ZoomToFit);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected);
        UndoCommand = new RelayCommand(Undo, () => Service.UndoRedo.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => Service.UndoRedo.CanRedo);
        StepContinueCommand = new AsyncRelayCommand(StepContinueSelectedAsync, CanStepContinue);
        ContinueFromNodeCommand = new AsyncRelayCommand<NodeViewModel>(
            ContinueFromNodeAsync,
            CanContinueFromNode);
        ExportSvgCommand = new RelayCommand<FrameworkElement?>(ExportSvg);
        CopyCommand = new RelayCommand(CopySelected);
        PasteCommand = new RelayCommand(PasteAtCenter);
        DeleteConnectionCommand = new RelayCommand<ConnectionModel?>(DeleteConnection);
        AddInputPortCommand = new RelayCommand<NodeViewModel>(AddInputPort);
        AddOutputPortCommand = new RelayCommand<NodeViewModel>(AddOutputPort);
        CopyNodeCommand = new RelayCommand<NodeViewModel>(node => { SelectedNode = node; CopySelected(); });
        DeleteNodeCommand = new RelayCommand<NodeViewModel>(node => { SelectedNode = node; DeleteSelected(); });
        SetNormalPermissionCommand = new RelayCommand<NodeViewModel>(node => SetPermission(node, NodePermission.Normal));
        SetReadOnlyPermissionCommand = new RelayCommand<NodeViewModel>(node => SetPermission(node, NodePermission.ReadOnly));
        SetDisabledPermissionCommand = new RelayCommand<NodeViewModel>(node => SetPermission(node, NodePermission.Disabled));
        MoveToInactiveContainerCommand = new RelayCommand<NodeViewModel>(MoveToInactiveContainer);
        RestoreFromInactiveContainerCommand = new RelayCommand<NodeViewModel>(RestoreFromInactiveContainer);
    }
}
