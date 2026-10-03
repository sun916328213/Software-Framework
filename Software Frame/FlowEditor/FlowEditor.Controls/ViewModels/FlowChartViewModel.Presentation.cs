// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Services.Editing。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Services.Editing;

namespace FlowEditor.Controls.ViewModels;

/// <summary>包含画布视口、日志、标签标题及导出相关的表现层入口。</summary>
public partial class FlowChartViewModel
{
    /// <summary>请求视图按给定内容边界执行自适应缩放。</summary>
    public event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>? ZoomToFitRequested
    {
        add => _canvas.ZoomToFitRequested += value;
        remove => _canvas.ZoomToFitRequested -= value;
    }

    /// <summary>追加日志到输出面板。</summary>
    public void AppendLog(string message) => _output.Append(message);

    /// <summary>查找节点。</summary>
    public NodeViewModel? FindNode(string nodeId) => _canvas.FindNode(nodeId);

    /// <summary>重命名标签页标题（由 View 层双击标签编辑触发）。</summary>
    public void RenameTab(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return;
        if (string.Equals(Model.TabTitle, newTitle, StringComparison.Ordinal)) return;
        if (Model.IsRunning || Model.IsLoopRunning)
        {
            Model.StatusText = "流程执行期间不能重命名";
            return;
        }

        SetTabTitle(newTitle, markModified: true);
    }

    /// <summary>统一变更标签标题；日志路由生命周期由模型属性变更观察器维护。</summary>
    /// <param name="newTitle">新的标签页标题。</param>
    /// <param name="markModified">是否将该变更记为用户编辑。</param>
    internal void SetTabTitle(string newTitle, bool markModified)
    {
        if (string.IsNullOrWhiteSpace(newTitle) ||
            string.Equals(Model.TabTitle, newTitle, StringComparison.Ordinal))
            return;

        Model.TabTitle = newTitle;
        if (markModified)
            Model.IsModified = true;
    }

    /// <summary>更新命令可执行状态。</summary>
    public void NotifyCanExecuteChanged()
    {
        RunCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        LoopRunCommand.NotifyCanExecuteChanged();
        LoopStopCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        StepContinueCommand.NotifyCanExecuteChanged();
        ContinueFromNodeCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>根据节点位置自动调整画布尺寸。</summary>
    private void RecalculateCanvasSize()
    {
        if (Nodes.Count == 0)
        {
            Model.CanvasWidth = FlowChartModel.MinCanvasWidth;
            Model.CanvasHeight = FlowChartModel.MinCanvasHeight;
            return;
        }
        var maxX = Nodes.Max(n => n.X + FlowCanvasSession.GetVisualWidth(n.Model)) +
            FlowChartModel.CanvasPadding;
        var maxY = Nodes.Max(n => n.Y + FlowCanvasSession.GetVisualHeight(n.Model)) +
            FlowChartModel.CanvasPadding;
        Model.CanvasWidth = Math.Max(maxX, FlowChartModel.MinCanvasWidth);
        Model.CanvasHeight = Math.Max(maxY, FlowChartModel.MinCanvasHeight);
    }

    /// <summary>以 2 倍分辨率导出指定画布区域，背景透明。</summary>
    public void ExportPng(FrameworkElement? element, Rect contentBounds)
    {
        if (element != null)
            _exportService.ExportToPngWithDialog(element, contentBounds, 2.0);
    }

    /// <summary>PNG 应使用完整逻辑画布范围，与视口和滚动位置无关。</summary>
    public Rect GetCanvasExportBounds() =>
        new(0, 0, Model.CanvasWidth, Model.CanvasHeight);

    /// <summary>导出 SVG（由 View 层调用）。</summary>
    public void ExportSvg(FrameworkElement? element)
    {
        if (element != null) _exportService.ExportToSvgWithDialog(element);
    }
}
