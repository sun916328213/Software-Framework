using System.Collections.Generic;

namespace ScriptEditor.Models;

/// <summary>
/// 脚本编辑器持久化状态：编辑器自身的完整数据（代码 + DLL 引用）。
/// 作为单个整体对象随流程图序列化保存/还原，宿主节点无需关心其内部构成。
/// </summary>
public class ScriptEditorState
{
    /// <summary>脚本源代码。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>用户 DLL 引用路径列表。</summary>
    public List<string> References { get; set; } = new();
}