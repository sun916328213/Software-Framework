// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：RuntimeReferenceCache。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、Microsoft.CodeAnalysis。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ScriptEditor.Runtime.Services;

/// <summary>
/// 运行时引用缓存：框架程序集、宿主目录程序集、默认引用的统一解析与缓存。
/// </summary>
/// <remarks>
/// P5 产物：原来 <c>CustomRoslynHost</c> 中的进程静态 Lazy 缓存下沉到此，
/// 编辑器工作区（<c>CreateProject</c>）与无头编译（<c>BuildScript</c>）
/// 经同一入口取引用集，保证两者一致；路径去重策略集中一处。
/// </remarks>
public sealed class RuntimeReferenceCache
{
    public static RuntimeReferenceCache Shared { get; } = new();

    private readonly Lazy<string[]> _frameworkDirectories = new(ResolveFrameworkDirectories);
    private readonly Lazy<IReadOnlyList<MetadataReference>> _frameworkReferences;
    private readonly Lazy<IReadOnlyList<MetadataReference>> _hostDirectoryReferences;

    /// <summary>
    /// 初始化 RuntimeReferenceCache 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public RuntimeReferenceCache()
    {
        _frameworkReferences = new Lazy<IReadOnlyList<MetadataReference>>(ResolveFrameworkReferences);
        _hostDirectoryReferences = new Lazy<IReadOnlyList<MetadataReference>>(ResolveHostDirectoryReferences);
    }

    /// <summary>框架程序集引用（延迟解析并缓存）。</summary>
    public IReadOnlyList<MetadataReference> FrameworkReferences => _frameworkReferences.Value;

    /// <summary>宿主目录程序集引用（延迟解析并缓存）。</summary>
    public IReadOnlyList<MetadataReference> HostDirectoryReferences => _hostDirectoryReferences.Value;

    /// <summary>安全解析引用显示路径为绝对路径，非法时回退原始值。</summary>
    public static string ResolveReferencePath(string display)
    {
        try { return Path.GetFullPath(display); }
        catch { return display; }
    }

    /// <summary>从元数据引用集合收集已注入路径（去重/diff 用）。</summary>
    public static HashSet<string> CollectReferencePaths(IEnumerable<MetadataReference> references)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            if (!string.IsNullOrWhiteSpace(reference.Display))
                paths.Add(ResolveReferencePath(reference.Display));
        }
        return paths;
    }

    /// <summary>
    /// 脚本编译/执行时额外注入的引用：运行时框架程序集 + 宿主程序目录 DLL（按路径去重）。
    /// 与编辑器工作区的引用集保持一致。
    /// </summary>
    public IReadOnlyList<MetadataReference> GetAdditionalDefaultReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();
        foreach (var reference in _frameworkReferences.Value.Concat(_hostDirectoryReferences.Value))
        {
            if (string.IsNullOrWhiteSpace(reference.Display)) continue;
            if (paths.Add(ResolveReferencePath(reference.Display)))
                references.Add(reference);
        }
        return references;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveFrameworkReferences。
    /// 正文中的关键协作调用包括 Empty、EnumerateFiles、GetAssemblyName、Add、CreateFromFile；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private IReadOnlyList<MetadataReference> ResolveFrameworkReferences()
    {
        var directories = _frameworkDirectories.Value;
        if (directories.Length == 0) return Array.Empty<MetadataReference>();

        // 按简单名称收集首选路径：后出现的 WindowsDesktop.App 实现覆盖
        // NETCore.App 中的同名类型转发门面，否则报 "引用声称定义在 WindowsBase 中但未能找到"。
        var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frameworkDir in directories)
        {
            foreach (var dllPath in Directory.EnumerateFiles(frameworkDir, "*.dll"))
            {
                string assemblyName;
                try { assemblyName = AssemblyName.GetAssemblyName(dllPath).Name ?? string.Empty; }
                catch { continue; }
                chosen[assemblyName] = dllPath;
            }
        }

        var references = new List<MetadataReference>(chosen.Count);
        foreach (var dllPath in chosen.Values)
        {
            try { references.Add(MetadataReference.CreateFromFile(dllPath)); }
            catch { /* 单个程序集失败不影响整体 */ }
        }
        return references;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveHostDirectoryReferences。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Exists、Empty、CollectFrameworkAssemblyNames、EnumerateFiles；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private IReadOnlyList<MetadataReference> ResolveHostDirectoryReferences()
    {
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
            return Array.Empty<MetadataReference>();

        // 宿主目录中与框架重名的程序集跳过（框架版本优先），避免同程序集多路径重复引用
        var frameworkNames = CollectFrameworkAssemblyNames();

        var references = new List<MetadataReference>();
        foreach (var dllPath in Directory.EnumerateFiles(baseDir, "*.dll"))
        {
            string assemblyName;
            try { assemblyName = AssemblyName.GetAssemblyName(dllPath).Name ?? string.Empty; }
            catch { continue; }
            if (frameworkNames.Contains(assemblyName)) continue;
            try { references.Add(MetadataReference.CreateFromFile(dllPath)); }
            catch { /* 忽略 */ }
        }
        return references;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CollectFrameworkAssemblyNames。
    /// 正文中的关键协作调用包括 Exists、EnumerateFiles、GetAssemblyName、IsNullOrWhiteSpace、Add；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private HashSet<string> CollectFrameworkAssemblyNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frameworkDir in _frameworkDirectories.Value)
        {
            if (!Directory.Exists(frameworkDir)) continue;
            foreach (var dllPath in Directory.EnumerateFiles(frameworkDir, "*.dll"))
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(dllPath).Name;
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name);
                }
                catch { /* 忽略 native 运行时 dll */ }
            }
        }
        return names;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveFrameworkDirectories。
    /// 正文中的关键协作调用包括 ResolveCoreFrameworkDirectory、IsNullOrWhiteSpace、Add、ResolveDesktopFrameworkDirectory、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    private static string[] ResolveFrameworkDirectories()
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coreDir = ResolveCoreFrameworkDirectory();
        if (!string.IsNullOrWhiteSpace(coreDir))
            directories.Add(coreDir);
        var desktopDir = ResolveDesktopFrameworkDirectory(coreDir);
        if (!string.IsNullOrWhiteSpace(desktopDir))
            directories.Add(desktopDir);
        return directories.ToArray();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveCoreFrameworkDirectory。
    /// 正文中的关键协作调用包括 GetDirectoryName、Exists、GetRuntimeDirectory；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static string? ResolveCoreFrameworkDirectory()
    {
        try
        {
            var consoleDir = Path.GetDirectoryName(typeof(Console).Assembly.Location);
            if (consoleDir != null && Directory.Exists(consoleDir))
                return consoleDir;
        }
        catch { }
        try
        {
            var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            if (runtimeDir != null && Directory.Exists(runtimeDir))
                return runtimeDir;
        }
        catch { }
        try
        {
            var objectDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (objectDir != null && Directory.Exists(objectDir))
                return objectDir;
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveDesktopFrameworkDirectory。
    /// 正文中的关键协作调用包括 FirstOrDefault、GetAssemblies、Equals、GetName、GetDirectoryName；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static string? ResolveDesktopFrameworkDirectory(string? coreDir)
    {
        // 保持 Runtime 可在无 WPF 环境编译运行：按程序集名查找
        try
        {
            var desktopAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "WindowsBase", StringComparison.OrdinalIgnoreCase));
            var desktopDir = desktopAssembly is null ? null : Path.GetDirectoryName(desktopAssembly.Location);
            if (desktopDir != null && Directory.Exists(desktopDir))
                return desktopDir;
        }
        catch { }
        if (!string.IsNullOrWhiteSpace(coreDir))
        {
            try
            {
                var sharedRoot = Path.GetDirectoryName(Path.GetDirectoryName(coreDir));
                var version = Path.GetFileName(coreDir);
                var candidate = Path.Combine(sharedRoot, "Microsoft.WindowsDesktop.App", version);
                if (Directory.Exists(candidate))
                    return candidate;
            }
            catch { }
        }
        return null;
    }
}
