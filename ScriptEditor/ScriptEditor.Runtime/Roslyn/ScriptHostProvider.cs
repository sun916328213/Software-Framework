using System.Reflection;
using RoslynPad.Roslyn;

namespace ScriptEditor.Services;

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
