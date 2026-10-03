// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptHostProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、RoslynPad.Roslyn。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using RoslynPad.Roslyn;

namespace ScriptEditor.Runtime.Services;

/// <summary>
/// 共享 Roslyn 宿主的静态提供者。
/// </summary>
/// <remarks>
/// 作为前后端分离后的唯一宿主入口：编辑器（ScriptEditorService）与无头执行
/// （ScriptEngine）共用同一个 CustomRoslynHost 实例，避免各自重复初始化
/// Roslyn 分析与编译管线（首次构建开销大，仅初始化一次）。
/// </remarks>
public static class ScriptHostProvider
{
    /// <summary>共享 CustomRoslynHost 实例（延迟到首次使用时创建）。</summary>
    private static readonly Lazy<CustomRoslynHost> SharedLazy = new(() => new CustomRoslynHost(
        additionalAssemblies: GetHostAssemblies(),
        references: RoslynHostReferences.NamespaceDefault.With(
            typeNamespaceImports: new[]
            {
                typeof(Console),
                typeof(System.Collections.Generic.List<>),
                typeof(System.Collections.ObjectModel.ObservableCollection<>),
                typeof(System.Linq.Enumerable),
                typeof(System.Text.StringBuilder),
                typeof(System.Text.RegularExpressions.Regex),
                typeof(System.Threading.Tasks.Task),
            })));

    /// <summary>获取共享宿主实例。</summary>
    public static CustomRoslynHost Shared => SharedLazy.Value;

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetHostAssemblies。
    /// 正文中的关键协作调用包括 Load；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static IEnumerable<Assembly> GetHostAssemblies()
    {
        yield return typeof(ScriptHostProvider).Assembly;
        foreach (var name in new[] { "RoslynPad.Roslyn.Windows", "RoslynPad.Editor.Windows" })
        {
            Assembly? assembly = null;
            try
            {
                assembly = Assembly.Load(name);
            }
            catch (System.IO.FileNotFoundException) { }
            if (assembly != null) yield return assembly;
        }
    }
}
