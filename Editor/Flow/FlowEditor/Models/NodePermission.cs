namespace FlowEditor.Models;

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
