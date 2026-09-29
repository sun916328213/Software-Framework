using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynPad.Roslyn;
using RoslynPad.Roslyn.Diagnostics;
using ScriptEditor.Options;

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
/// <para>P5：框架/宿主引用枚举下沉到 <see cref="RuntimeReferenceCache"/>；
/// 隐式 using 收敛到 <see cref="ScriptRuntimeOptions"/>（实例 <see cref="Options"/>），
/// 本类的进程静态 GlobalImports 入口仅为兼容保留。</para>
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.1</version>
public class CustomRoslynHost : RoslynHost
{
    /// <summary>运行时选项：隐式 using 的显式持有者（P5，替代隐藏的进程静态写入点）。</summary>
    /// <remarks>
    /// 默认为进程共享 <see cref="ScriptRuntimeOptions.Default"/>；
    /// 需要任务级隔离时由调用方装配 <see cref="ScriptProjectContext"/> 并经
    /// 无头执行管线传入，会话编译只读自己任务的集合。
    /// </remarks>
    public ScriptRuntimeOptions Options { get; set; } = ScriptRuntimeOptions.Default;

    private static RuntimeReferenceCache ReferenceCache => RuntimeReferenceCache.Shared;

    /// <summary>标记分析器是否已首次添加（后续 CreateProject 复用，避免重复初始化）。</summary>
    private bool _addedAnalyzers;

    /// <summary>默认强制注入的元数据引用（从基类 DefaultReferences 快照）。</summary>
    private readonly IReadOnlyList<MetadataReference> _enforceReferences;

    /// <summary>默认强制注入的 using 导入（从基类 DefaultImports 快照）。</summary>
    private readonly IReadOnlyList<string> _enforceImports;

    // ════════════════════════════════════════════════════════════════
    //  全局隐式 using（类似 SDK 的 ImplicitUsings / VS 隐式 global using）
    //  P5：收敛到 ScriptRuntimeOptions.Default，本类仅保留兼容入口。
    // ════════════════════════════════════════════════════════════════

    /// <summary>当前全局隐式 using 快照（兼容入口，读取进程默认选项）。</summary>
    public static IReadOnlyList<string> GlobalImports => ScriptRuntimeOptions.Default.Snapshot();

    /// <summary>注册单个全局隐式 using（幂等，空值忽略）。见 <see cref="AddGlobalImports"/>。</summary>
    public static void AddGlobalImport(string ns) => ScriptRuntimeOptions.Default.Add(ns);

    /// <summary>批量注册全局隐式 using（幂等，空值/重复忽略）。</summary>
    public static void AddGlobalImports(IEnumerable<string>? namespaces)
        => ScriptRuntimeOptions.Default.AddRange(namespaces);

    /// <summary>批量注册全局隐式 using（幂等，空值/重复忽略）。</summary>
    public static void AddGlobalImports(params string[] namespaces)
        => ScriptRuntimeOptions.Default.AddRange(namespaces);

    /// <summary>
    /// 自动引入：从脚本源码提取 using 指令并注册为全局隐式 using。
    /// 在任务加载/文件夹回填/保存时调用，把现有脚本的 using 收敛为全局集合，
    /// 之后所有脚本免写这些指令——名单随脚本演进而自动更新，无需手工维护。
    /// </summary>
    /// <remarks>P5 建议：多任务场景优先使用任务级 <see cref="ScriptProjectContext"/>，
    /// 避免不同任务经此进程静态入口互相污染。</remarks>
    public static void AddGlobalImportsFromSource(string? sourceCode)
        => ScriptRuntimeOptions.Default.AddFromSource(sourceCode);

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
    /// 隐式 using 取自本实例 <see cref="Options"/>（默认即进程默认选项），
    /// 实例创建之后再注册也照样生效。
    /// </summary>
    protected override CompilationOptions CreateCompilationOptions(
        DocumentCreationArgs args, bool addDefaultImports)
    {
        var options = (CSharpCompilationOptions)base.CreateCompilationOptions(args, addDefaultImports);
        return options.WithUsings(_enforceImports.Concat(Options.Snapshot()).Distinct());
    }

    /// <summary>
    /// 创建项目时注入：默认引用、运行时框架程序集、用户添加的额外引用。
    /// 所有引用均经过去重处理（框架/宿主引用集由 <see cref="RuntimeReferenceCache"/> 统一提供）。
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
        project = AddReferencesWithDedup(project, ReferenceCache.FrameworkReferences, existingPaths);

        // 3) 自动注入宿主程序目录下的所有 DLL（脚本在宿主程序下执行，可直接使用宿主类型）
        project = AddReferencesWithDedup(project, ReferenceCache.HostDirectoryReferences, existingPaths);

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

    // ════════════════════════════════════════════════════════════════
    //  内部共享辅助方法（P5：实现下沉到 RuntimeReferenceCache，本类仅保留兼容入口）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 安全解析引用显示路径为绝对路径，路径非法时回退到原始显示值。
    /// </summary>
    /// <param name="display">引用的 Display 属性值（通常是文件路径）。</param>
    /// <returns>解析后的绝对路径，或原始 display 值。</returns>
    public static string ResolveReferencePath(string display)
        => RuntimeReferenceCache.ResolveReferencePath(display);

    /// <summary>
    /// 获取脚本编译/执行时需额外注入的引用：运行时框架程序集 + 宿主程序目录下的全部 DLL。
    /// 与编辑项目的引用集保持一致（按路径去重）。
    /// </summary>
    public static IReadOnlyList<MetadataReference> GetAdditionalDefaultReferences()
        => ReferenceCache.GetAdditionalDefaultReferences();

    /// <summary>
    /// 从元数据引用集合中收集所有已注入的引用路径（用于去重和 diff 同步）。
    /// </summary>
    /// <param name="references">元数据引用集合。</param>
    /// <returns>大小写不敏感的路径集合。</returns>
    public static HashSet<string> CollectReferencePaths(
        IEnumerable<MetadataReference> references)
        => RuntimeReferenceCache.CollectReferencePaths(references);
}
