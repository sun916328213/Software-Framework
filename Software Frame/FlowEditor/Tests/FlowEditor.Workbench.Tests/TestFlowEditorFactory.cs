// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TestFlowEditorFactory、SilentDialogService、NoOpExportService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Services。导入不代表所有类型都会在运行时加载。
using System.Windows;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Workbench.Tests;

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
        /// <remarks>
        /// 测试相关入口 ShowOpenFileDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowSaveFileDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowConfirm；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool? ShowConfirm(string message, string title) => null;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ConfirmYesNo；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool ConfirmYesNo(string message, string title) => false;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowInfo；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public void ShowInfo(string message, string title = "提示")
        {
        }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowError；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public void ShowError(string message, string title = "错误")
        {
        }
    }

    /// <summary>忽略所有导出请求并返回取消结果的测试 adapter。</summary>
    private sealed class NoOpExportService : IExportService
    {
        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ExportToPngWithDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool ExportToPngWithDialog(FrameworkElement element, Rect contentBounds, double scale = 2)
            => false;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ExportToSvgWithDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool ExportToSvgWithDialog(FrameworkElement element) => false;
    }
}
