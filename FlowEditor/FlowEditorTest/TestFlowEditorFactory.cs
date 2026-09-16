using System.Windows;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Services;
using FlowEditor.Services.Storage;
using FlowEditor.ViewModels;

namespace FlowEditorTest;

/// <summary>集中创建带完整无头依赖的流程服务与 ViewModel 测试对象。</summary>
internal static class TestFlowEditorFactory
{
    /// <summary>共享的静默对话框 adapter。</summary>
    public static IDialogService Dialogs { get; } = new SilentDialogService();

    /// <summary>共享的无操作导出 adapter。</summary>
    public static IExportService Exports { get; } = new NoOpExportService();

    /// <summary>创建具有完整无头依赖的流程会话。</summary>
    /// <param name="documentStore">可选的文档存储覆盖。</param>
    /// <param name="nodeEditorLauncher">可选的节点编辑器覆盖。</param>
    /// <param name="nodeNotifications">可选的节点提示覆盖。</param>
    /// <param name="flowLogSink">可选的结构化流程日志接收器。</param>
    /// <returns>可直接运行和持久化的流程服务。</returns>
    public static FlowChartService CreateService(
        IFlowDocumentStore? documentStore = null,
        INodeEditorLauncher? nodeEditorLauncher = null,
        INodeNotificationSink? nodeNotifications = null,
        IFlowLogSink? flowLogSink = null)
        => new(
            documentStore ?? new InMemoryFlowDocumentStore(),
            new TestFlowChartExecutionEngine(),
            Dialogs,
            nodeEditorLauncher,
            nodeNotifications,
            flowLogSink: flowLogSink);

    /// <summary>创建绑定到指定流程会话的 ViewModel。</summary>
    /// <param name="service">流程会话；空值时自动创建无头会话。</param>
    /// <returns>具有完整依赖的流程 ViewModel。</returns>
    public static FlowChartViewModel CreateViewModel(FlowChartService? service = null)
        => new(service ?? CreateService(), Exports, Dialogs, new ConnectionService());

    /// <summary>不会打开窗口或选择文件的对话框测试 adapter。</summary>
    private sealed class SilentDialogService : IDialogService
    {
        /// <inheritdoc />
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;

        /// <inheritdoc />
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;

        /// <inheritdoc />
        public bool? ShowConfirm(string message, string title) => null;

        /// <inheritdoc />
        public bool ConfirmYesNo(string message, string title) => false;

        /// <inheritdoc />
        public void ShowInfo(string message, string title = "提示")
        {
        }

        /// <inheritdoc />
        public void ShowError(string message, string title = "错误")
        {
        }
    }

    /// <summary>忽略所有导出请求并返回取消结果的测试 adapter。</summary>
    private sealed class NoOpExportService : IExportService
    {
        /// <inheritdoc />
        public bool ExportToPngWithDialog(FrameworkElement element, Rect contentBounds, double scale = 2)
            => false;

        /// <inheritdoc />
        public bool ExportToSvgWithDialog(FrameworkElement element) => false;
    }
}
