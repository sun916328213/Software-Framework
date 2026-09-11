using System.Collections.ObjectModel;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;

namespace FlowEditor.Interfaces;

/// <summary>
/// 流程图服务接口。
/// 声明流程图的核心业务能力，供 ViewModel、NodeModel 等通过接口依赖，
/// 避免上层与具体服务实现紧耦合，也便于依赖注入与单元测试。
/// </summary>
public interface IFlowChartService
{
    /// <summary>流程图运行时模型。</summary>
    FlowChartModel Model { get; }

    /// <summary>画布上所有节点。</summary>
    ObservableCollection<NodeViewModel> Nodes { get; }

    /// <summary>所有连线。</summary>
    ObservableCollection<ConnectionModel> Connections { get; }

    /// <summary>撤销/重做服务。</summary>
    IUndoRedoService UndoRedo { get; }

    /// <summary>宿主提供的节点专用编辑器启动器。</summary>
    INodeEditorLauncher NodeEditorLauncher { get; }

    /// <summary>宿主提供的节点信息提示通道。</summary>
    INodeNotificationSink NodeNotifications { get; }

    /// <summary>当前文件路径。</summary>
    string? CurrentFilePath { get; set; }

    /// <summary>流程名（宿主设置，如 AutoFlow / InitializeFlow）；用于把节点日志路由到 D:\DeviceData\Log\Flow\{流程名}\{任务名}。</summary>
    string? ProcessName { get; set; }

    /// <summary>执行流程图。</summary>
    Task RunAsync(Action<string> logCallback, bool debugMode = false);

    /// <summary>循环执行流程图。</summary>
    Task RunLoopAsync(Action<string> logCallback);

    /// <summary>停止执行。</summary>
    void Stop();

    /// <summary>停止循环执行。</summary>
    void StopLoop();

    /// <summary>单步继续（调试模式）。</summary>
    void StepContinue();

    /// <summary>仅执行用户当前选中的一个节点，不沿连线调度下游。</summary>
    Task StepContinueAsync(NodeViewModel node, Action<string> logCallback);

    /// <summary>从选中节点恢复或跳转执行，并沿当前作用域及外层后继继续。</summary>
    Task ContinueFromNodeAsync(NodeViewModel node, Action<string> logCallback);

    /// <summary>调试控制器：断点与单步门闩（P4）。</summary>
    DebugController Debug { get; }

    /// <summary>画布执行的日志/数据追踪器（P4）。</summary>
    ExecutionJournal Journal { get; }

    /// <summary>将当前画布编译为不可变 FlowProgram（P3，用于创建隔离运行实例）。</summary>
    FlowProgram CompileProgram();

    /// <summary>放大。</summary>
    void ZoomIn();

    /// <summary>缩小。</summary>
    void ZoomOut();

    /// <summary>重置缩放。</summary>
    void ZoomReset();

    /// <summary>自适应画布。</summary>
    void ZoomToFit();

    /// <summary>自适应画布请求事件。</summary>
    event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>? ZoomToFitRequested;

    /// <summary>保存流程图到文件。</summary>
    bool Save(string? filePath = null);

    /// <summary>从文件加载流程图。</summary>
    bool Load(string filePath);

    /// <summary>追加日志到输出面板。</summary>
    void AppendLog(string message);

    /// <summary>立即将日志缓冲冲刷到输出面板（节流期间的最后一批日志在执行结束时调用）。</summary>
    void FlushLog();

    /// <summary>清空日志缓冲和当前输出栏文本。</summary>
    void ClearOutput();

    /// <summary>查找节点。</summary>
    NodeViewModel? FindNode(string nodeId);

    /// <summary>清空画布。</summary>
    void ClearAll();

    /// <summary>
    /// 是否为批量操作（加载流程/清空画布/撤销重做）：期间节点先 detach 再 re-attach，
    /// 视觉工具不得在 detach 时移除，否则重载后无法按名找回导致数据丢失。
    /// </summary>
    bool IsBatchOperation { get; set; }

    /// <summary>从节点文档重建节点模型（用于 ToolBlock 内部节点还原）。</summary>
    NodeModel RebuildNodeFromDocument(NodeDocumentModel doc);

    /// <summary>从节点模型构建节点文档（用于 ToolBlock 内部节点序列化）。</summary>
    NodeDocumentModel BuildNodeDocument(NodeModel node);

    /// <summary>添加节点（NodeViewModel 重载）。</summary>
    void AddNode(NodeViewModel nodeVm);

    /// <summary>添加节点模型并返回 NodeViewModel。</summary>
    NodeViewModel AddNode(NodeModel model);

    /// <summary>从画布移除节点，并触发节点卸载回调。</summary>
    void RemoveNode(NodeViewModel nodeVm);

    /// <summary>捕获当前画布状态快照（用于撤销/重做）。</summary>
    string CaptureSnapshot();

    /// <summary>应用画布状态快照（用于撤销/重做还原）。</summary>
    void ApplySnapshot(string json);
}
