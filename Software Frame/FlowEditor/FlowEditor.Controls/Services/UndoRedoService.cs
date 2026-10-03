// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：UndoRedoService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Services;

/// <summary>
/// 撤销/重做服务。
/// 记录画布操作历史，支持撤销和重做。
/// 通过 BeforeData/AfterData 保存操作前后的完整画布快照，
/// 由 FlowChartViewModel 在撤销/重做时调用 IFlowChartService 应用快照完成真实还原。
/// </summary>
public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<UndoRedoAction> _undoStack = new();
    private readonly Stack<UndoRedoAction> _redoStack = new();

    /// <summary>最大缓存步数，默认 50。</summary>
    public int MaxHistorySteps { get; set; } = 50;

    /// <summary>是否可撤销。</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>是否可重做。</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>撤销栈变化事件。</summary>
    public event Action? StackChanged;

    /// <summary>通知栈变化（供外部在快照应用后刷新命令状态）。</summary>
    public void NotifyStackChanged() => StackChanged?.Invoke();

    /// <summary>记录操作。</summary>
    public void Push(UndoRedoAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();

        // 限制最大缓存步数
        while (_undoStack.Count > MaxHistorySteps)
        {
            var temp = new Stack<UndoRedoAction>();
            while (_undoStack.Count > 1) temp.Push(_undoStack.Pop());
            _undoStack.Pop();
            while (temp.Count > 0) _undoStack.Push(temp.Pop());
        }

        StackChanged?.Invoke();
    }

    /// <summary>撤销操作。</summary>
    public UndoRedoAction? Undo()
    {
        if (_undoStack.Count == 0) return null;
        var action = _undoStack.Pop();
        _redoStack.Push(action);
        StackChanged?.Invoke();
        return action;
    }

    /// <summary>重做操作。</summary>
    public UndoRedoAction? Redo()
    {
        if (_redoStack.Count == 0) return null;
        var action = _redoStack.Pop();
        _undoStack.Push(action);
        StackChanged?.Invoke();
        return action;
    }

    /// <summary>清空所有历史。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StackChanged?.Invoke();
    }
}