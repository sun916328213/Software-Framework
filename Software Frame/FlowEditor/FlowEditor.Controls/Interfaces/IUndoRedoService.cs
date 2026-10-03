// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IUndoRedoService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Interfaces;

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
