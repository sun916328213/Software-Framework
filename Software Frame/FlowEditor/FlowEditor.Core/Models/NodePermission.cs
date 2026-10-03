// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodePermission。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core.Models;

/// <summary>节点权限标记。</summary>
public enum NodePermission
{
    /// <summary>正常：可编辑、拖动、删除、修改端口。</summary>
    Normal,
    /// <summary>只读：仅可查看，不可拖动、编辑属性、删除、修改端口。</summary>
    ReadOnly,
    /// <summary>禁用：完全禁用交互，灰色显示。</summary>
    Disabled
}
