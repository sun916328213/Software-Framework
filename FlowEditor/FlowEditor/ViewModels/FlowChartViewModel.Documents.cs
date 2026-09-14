using FlowEditor.Models;

namespace FlowEditor.ViewModels;

/// <summary>包含流程文档交互及撤销/重做事务编排。</summary>
public partial class FlowChartViewModel
{
    /// <summary>保存流程图。</summary>
    private void Save()
    {
        if (!string.IsNullOrEmpty(Model.CurrentFilePath))
        {
            _documents.Save(null);
            return;
        }
        SaveAs();
    }

    /// <summary>另存为。</summary>
    private void SaveAs()
    {
        var path = _dialogService.ShowSaveFileDialog(
            "流程图文件 (*.flow)|*.flow|所有文件 (*.*)|*.*",
            Model.TabTitle + ".flow",
            ".flow");
        if (path != null)
            _documents.Save(path);
    }

    /// <summary>从文件加载流程图。</summary>
    private void Load()
    {
        var path = _dialogService.ShowOpenFileDialog("流程图文件 (*.flow)|*.flow|所有文件 (*.*)|*.*");
        if (path != null)
            _documents.Load(path);
    }

    /// <summary>清空画布（二次确认）。</summary>
    private void ClearAll()
    {
        var confirmed = _dialogService.ConfirmYesNo(
            "确定要删除所有流程吗？此操作不可撤销！", "确认删除");
        if (confirmed)
        {
            _canvas.ClearAll();
            RecalculateCanvasSize();
        }
    }

    /// <summary>创建撤销动作并捕获操作前快照。</summary>
    private UndoRedoAction CreateAction(
        UndoRedoActionType type, string description,
        string? nodeId = null, string? connectionId = null)
        => new()
        {
            ActionType = type,
            Description = description,
            NodeId = nodeId,
            ConnectionId = connectionId,
            BeforeData = _documents.CaptureSnapshot()
        };

    /// <summary>撤销最近一次画布编辑并恢复快照。</summary>
    private void Undo()
    {
        var action = UndoRedo.Undo();
        if (action == null) return;
        // 保存当前状态作为重做目标
        action.AfterData = _documents.CaptureSnapshot();
        _documents.ApplySnapshot(action.BeforeData!);
        Model.StatusText = $"已撤销: {action.Description}";
        Model.IsModified = true;
        RecalculateCanvasSize();
        NotifyCanExecuteChanged();
    }

    /// <summary>重做最近一次撤销的画布编辑。</summary>
    private void Redo()
    {
        var action = UndoRedo.Redo();
        if (action == null) return;
        // 保存当前状态作为撤销目标
        action.BeforeData = _documents.CaptureSnapshot();
        _documents.ApplySnapshot(action.AfterData!);
        Model.StatusText = $"已重做: {action.Description}";
        Model.IsModified = true;
        RecalculateCanvasSize();
        NotifyCanExecuteChanged();
    }
}
