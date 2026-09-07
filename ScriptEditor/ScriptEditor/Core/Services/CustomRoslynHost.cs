using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynPad.Editor;
using RoslynPad.Roslyn;
using RoslynPad.Roslyn.Diagnostics;

namespace ScriptEditor.Services;

/// <summary>
/// 自定义 Roslyn 宿主，扩展脚本引用管理和框架程序集自动发现。
/// </summary>
/// <remarks>
/// <para>核心职责：</para>
/// <list type="bullet">
///   <item>自动发现并注入 .NET 运行时框架程序集（System.Runtime.dll 等）</item>
///   <item>去重引用，避免重复添加已有的程序集</item>
///   <item>管理分析器（Analyzers）生命周期，仅首次添加</item>
/// </list>
/// <para>使用方式：由 ScriptEditorService 在构造函数中创建并持有。</para>
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.0</version>
public class CustomRoslynHost : RoslynHost
{
    /// <summary>延迟解析 .NET 运行时共享框架目录（NETCore.App + WindowsDesktop.App），避免启动阶段 IO 开销。</summary>
    private static readonly Lazy<string[]> FrameworkDirectories = new(() => ResolveFrameworkDirectories());

    /// <summary>
    /// 延迟枚举并缓存 .NET 运行时共享框架程序集（参考 VS 默认引用，覆盖整个共享框架，
    /// 例如 System.Runtime.dll、System.Collections.dll、System.ObjectModel.dll 等）。
    /// 同时包含 Microsoft.WindowsDesktop.App（WindowsBase.dll、PresentationCore.dll 等 WPF 程序集）。
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> FrameworkReferences =
        new(() => ResolveFrameworkReferences());

    /// <summary>
    /// 延迟枚举并缓存宿主程序运行目录下的全部托管程序集。
    /// 脚本最终在宿主程序下执行，因此自动加载程序目录下的所有 DLL，
    /// 使脚本可直接引用宿主应用及其依赖的类型。
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> HostDirectoryReferences =
        new(() => ResolveHostDirectoryReferences());

    /// <summary>标记分析器是否已首次添加（后续 CreateProject 复用，避免重复初始化）。</summary>
    private bool _addedAnalyzers;

    /// <summary>默认强制注入的元数据引用（从基类 DefaultReferences 快照）。</summary>
    private readonly IReadOnlyList<MetadataReference> _enforceReferences;

    /// <summary>默认强制注入的 using 导入（从基类 DefaultImports 快照）。</summary>
    private readonly IReadOnlyList<string> _enforceImports;

    // ════════════════════════════════════════════════════════════════
    //  全局隐式 using（类似 SDK 的 ImplicitUsings / VS 隐式 global using）
    // ════════════════════════════════════════════════════════════════

    /// <summary>全局隐式 using 集合：注册后所有脚本自动导入，源码无需再写对应 using 指令。</summary>
    private static readonly List<string> _globalImports = new();

    /// <summary>保护 <see cref="_globalImports"/> 的锁。</summary>
    private static readonly object _globalImportsLock = new();

    /// <summary>当前全局隐式 using 快照。</summary>
    public static IReadOnlyList<string> GlobalImports
    {
        get { lock (_globalImportsLock) return _globalImports.ToArray(); }
    }

    /// <summary>注册单个全局隐式 using（幂等，空值忽略）。见 <see cref="AddGlobalImports"/>。</summary>
    public static void AddGlobalImport(string ns) => AddGlobalImports(new[] { ns });

    /// <summary>批量注册全局隐式 using（幂等，空值/重复忽略）。</summary>
    public static void AddGlobalImports(IEnumerable<string>? namespaces)
    {
        if (namespaces == null) return;
        lock (_globalImportsLock)
        {
            foreach (var ns in namespaces)
            {
                var trimmed = ns?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (!_globalImports.Contains(trimmed))
                    _globalImports.Add(trimmed);
            }
        }
    }

    /// <summary>批量注册全局隐式 using（幂等，空值/重复忽略）。</summary>
    public static void AddGlobalImports(params string[] namespaces)
        => AddGlobalImports((IEnumerable<string>)namespaces);

    /// <summary>匹配脚本源码中的普通 using 指令（自动排除 using static 与 别名 X = ... 形式）。</summary>
    private static readonly System.Text.RegularExpressions.Regex UsingDirectivePattern =
        new(@"^\s*using\s+(?!static\b)([A-Za-z_][A-Za-z0-9_.]*)\s*;\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// 自动引入：从脚本源码提取 using 指令并注册为全局隐式 using。
    /// 在任务加载/文件夹回填/保存时调用，把现有脚本的 using 收敛为全局集合，
    /// 之后所有脚本免写这些指令——名单随脚本演进而自动更新，无需手工维护。
    /// </summary>
    public static void AddGlobalImportsFromSource(string? sourceCode)
    {
        if (string.IsNullOrEmpty(sourceCode)) return;

        var matches = UsingDirectivePattern.Matches(sourceCode);
        if (matches.Count == 0) return;

        var namespaces = new List<string>(matches.Count);
        foreach (System.Text.RegularExpressions.Match match in matches)
            namespaces.Add(match.Groups[1].Value);
        AddGlobalImports(namespaces);
    }

    /// <summary>
    /// 创建自定义 Roslyn 宿主。
    /// </summary>
    /// <param name="additionalAssemblies">额外需要加载的程序集列表。</param>
    /// <param name="references">命名空间引用配置。</param>
    /// <param name="disabledDiagnostics">禁用的诊断 ID 集合。</param>
    public CustomRoslynHost(
        IEnumerable<Assembly>? additionalAssemblies = null,
        RoslynHostReferences? references = null,
        ImmutableHashSet<string>? disabledDiagnostics = null)
        : base(additionalAssemblies, references, disabledDiagnostics)
    {
        _enforceReferences = DefaultReferences;
        _enforceImports = DefaultImports;
    }

    // ════════════════════════════════════════════════════════════════
    //  重写基类方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取解决方案分析器引用。
    /// 仅在首次创建项目时添加分析器，后续复用避免重复初始化开销。
    /// </summary>
    protected override IEnumerable<AnalyzerReference> GetSolutionAnalyzerReferences()
    {
        if (!_addedAnalyzers)
        {
            _addedAnalyzers = true;
            return base.GetSolutionAnalyzerReferences();
        }
        return Enumerable.Empty<AnalyzerReference>();
    }

    public override RoslynWorkspace CreateWorkspace()
    {
        var workspace = new RoslynWorkspace(HostServices, roslynHost: this);
        var diagnosticsUpdater = workspace.Services.GetRequiredService<IDiagnosticsUpdater>();
        diagnosticsUpdater.DisabledDiagnostics = DisabledDiagnostics;
        return workspace;
    }

    /// <summary>
    /// 创建编译选项时强制注入默认 using 导入，确保脚本中无需手动添加 using 指令。
    /// 运行时实时追加全局隐式 using（<see cref="GlobalImports"/>），
    /// 因此宿主实例创建之后再注册也照样生效。
    /// </summary>
    protected override CompilationOptions CreateCompilationOptions(
        DocumentCreationArgs args, bool addDefaultImports)
    {
        var options = (CSharpCompilationOptions)base.CreateCompilationOptions(args, addDefaultImports);
        return options.WithUsings(_enforceImports.Concat(GlobalImports).Distinct());
    }

    /// <summary>
    /// 创建项目时注入：默认引用、运行时框架程序集、用户添加的额外引用。
    /// 所有引用均经过去重处理。
    /// </summary>
    protected override Project CreateProject(
        Solution solution,
        DocumentCreationArgs args,
        CompilationOptions compilationOptions,
        Project? previousProject = null)
    {
        var project = base.CreateProject(solution, args, compilationOptions, previousProject);

        var existingPaths = GetExistingReferencePaths(project);

        // 1) 强制注入默认引用
        project = AddReferencesWithDedup(project, _enforceReferences, existingPaths);

        // 2) 自动发现并注入运行时框架程序集（参考 VS 默认引用，覆盖整个共享框架）
        project = AddReferencesWithDedup(project, FrameworkReferences.Value, existingPaths);

        // 3) 自动注入宿主程序目录下的所有 DLL（脚本在宿主程序下执行，可直接使用宿主类型）
        project = AddReferencesWithDedup(project, HostDirectoryReferences.Value, existingPaths);

        return project;
    }

    // ════════════════════════════════════════════════════════════════
    //  私有辅助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>收集项目中已有的引用路径，用于去重。</summary>
    private static HashSet<string> GetExistingReferencePaths(Project project)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in project.MetadataReferences)
        {
            if (!string.IsNullOrWhiteSpace(reference.Display))
            {
                try
                {
                    paths.Add(Path.GetFullPath(reference.Display));
                }
                catch
                {
                    // 路径格式异常时直接使用原始值
                    paths.Add(reference.Display);
                }
            }
        }
        return paths;
    }

    /// <summary>向项目批量添加引用，自动跳过已存在的路径。</summary>
    private static Project AddReferencesWithDedup(
        Project project,
        IReadOnlyList<MetadataReference> references,
        HashSet<string> existingPaths)
    {
        foreach (var reference in references)
        {
            if (!string.IsNullOrWhiteSpace(reference.Display))
            {
                try
                {
                    var fullPath = Path.GetFullPath(reference.Display);
                    if (existingPaths.Contains(fullPath)) continue;
                    existingPaths.Add(fullPath);
                }
                catch
                {
                    if (existingPaths.Contains(reference.Display)) continue;
                    existingPaths.Add(reference.Display);
                }
            }
            project = project.AddMetadataReference(reference);
        }
        return project;
    }

    /// <summary>
    /// 枚举共享框架目录中的托管程序集并创建元数据引用。
    /// 与 VS 默认引用保持一致：脚本中可直接使用整个 .NET 运行时 API，
    /// 其中 System.ObjectModel.dll 提供 System.Collections.ObjectModel 命名空间。
    /// </summary>
    private static IReadOnlyList<MetadataReference> ResolveFrameworkReferences()
    {
        var directories = FrameworkDirectories.Value;
        if (directories.Length == 0) return Array.Empty<MetadataReference>();

        // 按简单名称收集每个程序集的首选路径：
        // NETCore.App 中同名程序集可能是类型转发门面（如 WindowsBase.dll 只转发到
        // WindowsDesktop.App 的真实实现），后出现的 WindowsDesktop.App 实现必须覆盖门面，
        // 否则脚本编译报 "对类型 DependencyObject 的引用声称定义在 WindowsBase 中但未能找到"。
        var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frameworkDir in directories)
        {
            foreach (var dllPath in Directory.EnumerateFiles(frameworkDir, "*.dll"))
            {
                // 过滤 native 运行时 dll（coreclr.dll、clrjit.dll 等非托管文件）
                string assemblyName;
                try
                {
                    assemblyName = AssemblyName.GetAssemblyName(dllPath).Name ?? string.Empty;
                }
                catch
                {
                    continue;
                }

                // 后出现的目录（WindowsDesktop.App）覆盖先前目录（NETCore.App）的同名程序集
                chosen[assemblyName] = dllPath;
            }
        }

        var references = new List<MetadataReference>(chosen.Count);
        foreach (var dllPath in chosen.Values)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(dllPath));
            }
            catch
            {
                // 单个程序集加载失败时忽略，不影响整体
            }
        }
        return references;
    }

    /// <summary>
    /// 枚举宿主程序运行目录（AppContext.BaseDirectory）中的全部托管程序集并创建元数据引用。
    /// 脚本最终在宿主程序下执行，自动加载程序目录下所有 DLL，
    /// 使脚本可直接使用宿主应用及其依赖的类型。
    /// </summary>
    private static IReadOnlyList<MetadataReference> ResolveHostDirectoryReferences()
    {
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
            return Array.Empty<MetadataReference>();

        // 收集运行时框架程序集的简单名称，宿主目录中与之重名的程序集跳过，
        // 避免同一程序集不同路径产生重复引用（框架版本优先，宿主特有程序集全部加载）
        var frameworkNames = CollectFrameworkAssemblyNames();

        var references = new List<MetadataReference>();
        foreach (var dllPath in Directory.EnumerateFiles(baseDir, "*.dll"))
        {
            // 过滤 native dll（C++/驱动等非托管文件）
            string assemblyName;
            try
            {
                assemblyName = AssemblyName.GetAssemblyName(dllPath).Name ?? string.Empty;
            }
            catch
            {
                continue;
            }

            if (frameworkNames.Contains(assemblyName))
                continue;

            try
            {
                references.Add(MetadataReference.CreateFromFile(dllPath));
            }
            catch
            {
                // 单个程序集加载失败时忽略，不影响整体
            }
        }
        return references;
    }

    /// <summary>收集运行时框架程序集的简单名称集合（用于宿主目录重名程序集过滤）。</summary>
    private static HashSet<string> CollectFrameworkAssemblyNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frameworkDir in FrameworkDirectories.Value)
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
    /// 解析 .NET 运行时共享框架目录集合：
    /// 1) Microsoft.NETCore.App（基础框架程序集）
    /// 2) Microsoft.WindowsDesktop.App（WPF/WinForms 程序集，WindowsBase.dll 等）
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
    /// 解析 Microsoft.NETCore.App 目录。
    /// 依次尝试：Console 程序集目录 → RuntimeEnvironment → object 程序集目录。
    /// </summary>
    private static string? ResolveCoreFrameworkDirectory()
    {
        // 尝试 1：System.Console.dll 所在目录
        try
        {
            var consoleDir = Path.GetDirectoryName(typeof(Console).Assembly.Location);
            if (consoleDir != null && Directory.Exists(consoleDir))
                return consoleDir;
        }
        catch { /* Console 程序集目录解析失败，尝试下一策略 */ }

        // 尝试 2：运行时环境目录
        try
        {
            var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            if (runtimeDir != null && Directory.Exists(runtimeDir))
                return runtimeDir;
        }
        catch { /* 运行时环境目录解析失败，尝试下一策略 */ }

        // 尝试 3：System.Object 所在目录（最后手段）
        try
        {
            var objectDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (objectDir != null && Directory.Exists(objectDir))
                return objectDir;
        }
        catch { /* object 程序集目录解析失败，返回 null */ }

        return null;
    }

    /// <summary>
    /// 解析 Microsoft.WindowsDesktop.App 目录（包含 WindowsBase.dll、PresentationCore.dll 等 WPF 程序集，
    /// 其中 System.Windows.DependencyObject 定义在 WindowsBase.dll 中）。
    /// 依次尝试：DependencyObject 程序集目录 → 与 NETCore.App 同版本的 WindowsDesktop.App 目录。
    /// </summary>
    private static string? ResolveDesktopFrameworkDirectory(string? coreDir)
    {
        // 尝试 1：System.Windows.DependencyObject 程序集（WindowsBase.dll）所在目录
        try
        {
            var desktopDir = Path.GetDirectoryName(typeof(System.Windows.DependencyObject).Assembly.Location);
            if (desktopDir != null && Directory.Exists(desktopDir))
                return desktopDir;
        }
        catch { /* WPF 类型不可用，尝试下一策略 */ }

        // 尝试 2：与 NETCore.App 同版本号的 Microsoft.WindowsDesktop.App 目录
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
            catch { /* 目录推算失败，返回 null */ }
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════════
    //  内部共享辅助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 安全解析引用显示路径为绝对路径，路径非法时回退到原始显示值。
    /// </summary>
    /// <param name="display">引用的 Display 属性值（通常是文件路径）。</param>
    /// <returns>解析后的绝对路径，或原始 display 值。</returns>
    internal static string ResolveReferencePath(string display)
    {
        try { return Path.GetFullPath(display); }
        catch { return display; }
    }

    /// <summary>
    /// 获取脚本编译/执行时需额外注入的引用：运行时框架程序集 + 宿主程序目录下的全部 DLL。
    /// 与编辑项目的引用集保持一致（按路径去重）。
    /// </summary>
    public static IReadOnlyList<MetadataReference> GetAdditionalDefaultReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();
        foreach (var reference in FrameworkReferences.Value.Concat(HostDirectoryReferences.Value))
        {
            if (string.IsNullOrWhiteSpace(reference.Display)) continue;
            if (paths.Add(ResolveReferencePath(reference.Display)))
                references.Add(reference);
        }
        return references;
    }

    /// <summary>
    /// 从元数据引用集合中收集所有已注入的引用路径（用于去重和 diff 同步）。
    /// </summary>
    /// <param name="references">元数据引用集合。</param>
    /// <returns>大小写不敏感的路径集合。</returns>
    internal static HashSet<string> CollectReferencePaths(
        IEnumerable<MetadataReference> references)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            if (!string.IsNullOrWhiteSpace(reference.Display))
                paths.Add(ResolveReferencePath(reference.Display));
        }
        return paths;
    }
}