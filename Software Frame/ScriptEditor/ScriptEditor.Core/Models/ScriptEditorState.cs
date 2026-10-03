// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptEditorState。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using System.Collections.Generic;

namespace ScriptEditor.Core.Models;

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