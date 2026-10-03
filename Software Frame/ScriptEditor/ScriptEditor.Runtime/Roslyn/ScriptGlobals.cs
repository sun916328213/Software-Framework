// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptGlobals。成员说明可结合下方具体实现阅读。
namespace ScriptEditor.Runtime.Services;

/// <summary>
/// 脚本全局对象。Roslyn 脚本里可直接访问 <c>Inputs</c> 字段（dynamic），支持任意类型：
/// <list type="bullet">
///   <item><see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>：保留旧写法 <c>Inputs["key"]</c></item>
///   <item>任意 class：<c>Inputs.PropertyName</c>、<c>Inputs.Method()</c>，通过 dynamic 调度</item>
/// </list>
/// 静态 globalsType 保证缓存 key 稳定（始终是本类型），不会因运行时 inputData 类型变化而重新编译。
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>脚本输入参数。支持任意类型：IDictionary 走索引器，class 走 dynamic 成员访问。
    /// 实例形式 Script 的 suffix 注入代码（EnsureScriptRunInvoked）也通过本字段给实例 Inputs 赋值。</summary>
    public dynamic? Inputs { get; set; }
}
