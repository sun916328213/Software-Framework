using System.Windows;
using FlowEditor.Models;

namespace FlowEditor.Interfaces;

/// <summary>
/// 撤销/重做服务接口。
/// 记录画布操作历史，支持撤销和重做。
/// </summary>
public interface IUndoRedoService
{
    /// <summary>最大缓存步数。</summary>
    int MaxHistorySteps { get; set; }

    /// <summary>是否可撤销。</summary>
    bool CanUndo { get; }

    /// <summary>是否可重做。</summary>
    bool CanRedo { get; }

    /// <summary>撤销栈变化事件。</summary>
    event Action? StackChanged;

    /// <summary>记录操作。</summary>
    void Push(UndoRedoAction action);

    /// <summary>撤销操作。</summary>
    UndoRedoAction? Undo();

    /// <summary>重做操作。</summary>
    UndoRedoAction? Redo();

    /// <summary>清空所有历史。</summary>
    void Clear();
}
