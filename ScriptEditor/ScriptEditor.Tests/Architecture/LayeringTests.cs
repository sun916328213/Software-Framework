using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ScriptEditor.Tests.Architecture;

/// <summary>
/// P5 ScriptEditor 边界守卫：钉住 Core ← Runtime ← Controls ← Workbench 的分层方向，
/// 以及「逻辑测试线无 WPF 且直引 Core + Runtime」的测试拆分规则，防止架构回退。
/// </summary>
public sealed class LayeringTests
{
    /// <summary>ScriptEditor.sln 所在目录（即 ScriptEditor 模块根）。</summary>
    private static readonly string SolutionDirectory = LocateSolutionDirectory();

    /// <summary>Core 是纯契约/模型层：不引用任何工程，也不启用 WPF。</summary>
    [Fact]
    public void Core_project_has_no_project_references()
    {
        var csproj = LoadCsproj("ScriptEditor.Core", "ScriptEditor.Core.csproj");

        Assert.False(HasUseWpf(csproj), "ScriptEditor.Core 是纯逻辑层，不应启用 WPF。");
        Assert.Empty(ProjectReferences(csproj));
    }

    /// <summary>Runtime 只依赖 Core，禁止反向依赖 Controls / Workbench。</summary>
    [Fact]
    public void Runtime_project_references_core_only()
    {
        var csproj = LoadCsproj("ScriptEditor.Runtime", "ScriptEditor.Runtime.csproj");

        Assert.False(HasUseWpf(csproj), "ScriptEditor.Runtime 是运行时编排层，不应启用 WPF。");

        var projects = ProjectReferences(csproj);
        // 下限断言防假绿：必须存在对 Core 的直引。
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Core.csproj", StringComparison.Ordinal));
        Assert.Single(projects);
    }

    /// <summary>
    /// Controls 是 WPF 表现层。其引用面被精确白名单钉住：除 Core 契约外，另有两项
    /// <b>已登记的有意偏离</b>（2026-09-30）——
    /// <list type="number">
    ///   <item>直引 ScriptEditor.Runtime：编排服务（ScriptEditorService / ScriptManagerService /
    ///   FileService）与视图同处 Controls，直接持有 <c>IScriptRuntime</c>、<c>CustomRoslynHost</c>、
    ///   <c>ScriptProjectStore</c>/<c>ScriptFileStore</c> 等运行时实现，而非只面向 Core 契约；</item>
    ///   <item>直引重型 Extension 与 WPFCustomControls：复用通用能力与公共控件样式。</item>
    /// </list>
    /// 白名单之外的引用（尤其是反向依赖 Workbench 宿主）一律视为越界；一旦编排服务下沉到
    /// Runtime 完成收敛，本用例应改回「Controls 只依赖 Core」的强断言。
    /// </summary>
    [Fact]
    public void Controls_project_references_are_whitelisted()
    {
        var csproj = LoadCsproj("ScriptEditor.Controls", "ScriptEditor.Controls.csproj");

        Assert.True(HasUseWpf(csproj), "ScriptEditor.Controls 是 WPF 表现层，应启用 WPF。");
        Assert.Equal("ScriptEditor", AssemblyName(csproj));

        var expected = new[]
        {
            "Extension.csproj",
            "ScriptEditor.Core.csproj",
            "ScriptEditor.Runtime.csproj",
            "WPFCustomControls.csproj",
        };
        var actual = ProjectReferences(csproj).Select(FileName).OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(expected, actual);

        // 表现层不得反向依赖宿主。
        Assert.DoesNotContain(ProjectReferences(csproj), r => r.Contains("ScriptEditor.Workbench"));
    }

    /// <summary>
    /// 宿主是 Composition Root：必须显式组合被装配的层（Core / Runtime / Controls），
    /// 不靠传递依赖获得 Runtime —— 与 SecsGem / Device 的 Workbench 口径一致。
    /// </summary>
    [Fact]
    public void Workbench_project_directly_composes_core_layers()
    {
        var csproj = LoadCsproj("ScriptEditor.Workbench", "ScriptEditor.Workbench.csproj");

        Assert.Equal("WinExe", OutputType(csproj));

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Controls.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Runtime.csproj", StringComparison.Ordinal));
    }

    /// <summary>
    /// 逻辑测试线保持无界面：不启用 WPF、TFM 不含 windows，且只直引 Core + Runtime，
    /// 不经 Controls / Workbench 等顶层聚合项目间接获得依赖。
    /// </summary>
    [Fact]
    public void Logic_tests_line_stays_wpf_free_and_directly_references_core_and_runtime()
    {
        var csproj = LoadCsproj("ScriptEditor.Tests", "ScriptEditor.Tests.csproj");

        Assert.False(HasUseWpf(csproj), "ScriptEditor.Tests 是逻辑测试线，不应启用 WPF。");
        Assert.Equal("net9.0", TargetFramework(csproj));

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Core.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Runtime.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(projects, r => r.Contains("ScriptEditor.Controls"));
        Assert.DoesNotContain(projects, r => r.Contains("ScriptEditor.Workbench"));
    }

    /// <summary>
    /// 解决方案登记的每个项目路径都必须真实存在。2026-09-29 曾出现悬挂项目
    /// <c>ScriptEditor\ScriptEditor.csproj</c>（旧工程已由 ScriptEditor.Controls 取代），
    /// 导致 sln 入口构建失效；此守卫防止同类回退。
    /// </summary>
    [Fact]
    public void Solution_project_paths_all_exist_on_disk()
    {
        var sln = File.ReadAllText(Path.Combine(SolutionDirectory, "ScriptEditor.sln"));
        var declared = ProjectPathPattern.Matches(sln)
            .Select(match => match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

        // 下限断言防假绿：五个真实工程都应被解析到。
        Assert.Contains(declared, p => p.EndsWith("ScriptEditor.Core.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("ScriptEditor.Runtime.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("ScriptEditor.Controls.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("ScriptEditor.Workbench.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("ScriptEditor.Tests.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("ScriptEditor.Workbench.Tests.csproj", StringComparison.Ordinal));

        // 架构只认两条线（Xxx.Tests + Xxx.Workbench.Tests）：碎片工程不得再被登记。
        Assert.DoesNotContain(declared, p => p.EndsWith("ScriptEditor.Core.Tests.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(declared, p => p.EndsWith("ScriptEditor.Runtime.Tests.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(declared, p => p.EndsWith("ScriptEditor.Controls.Tests.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(declared, p => p.EndsWith("ScriptEditor.Host.Tests.csproj", StringComparison.Ordinal));

        var missing = declared
            .Where(relative => !File.Exists(Path.Combine(SolutionDirectory, relative)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(missing);
    }

    /// <summary>
    /// WPF 集成测试线必须显式启用 WPF，并组合被验证的 Controls（视图 / ViewModel）与
    /// Workbench（Prism / DI 装配入口），沿用 MotionControl / Communication / DataManager 口径。
    /// </summary>
    [Fact]
    public void Workbench_tests_line_enables_wpf_and_composes_controls_and_workbench()
    {
        var csproj = LoadCsproj("ScriptEditor.Workbench.Tests", "ScriptEditor.Workbench.Tests.csproj");

        Assert.True(HasUseWpf(csproj), "WPF 集成测试线必须显式 UseWPF=true。");

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Controls.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, r => r.EndsWith("ScriptEditor.Workbench.csproj", StringComparison.Ordinal));
    }

    /// <summary>
    /// Workbench 是 Prism 宿主：必须有 App.xaml（ApplicationDefinition）作为唯一入口，
    /// 且不得残留手工装配的 Program.cs —— 它会与 App.xaml 生成的 Main 冲突。
    /// </summary>
    [Fact]
    public void Workbench_is_prism_application_with_app_xaml_as_sole_entry()
    {
        var workbench = Path.Combine(SolutionDirectory, "ScriptEditor.Workbench");

        Assert.True(File.Exists(Path.Combine(workbench, "App.xaml")), "ScriptEditor.Workbench 应提供 App.xaml。");
        Assert.True(File.Exists(Path.Combine(workbench, "App.xaml.cs")), "ScriptEditor.Workbench 应提供 App.xaml.cs。");
        Assert.False(
            File.Exists(Path.Combine(workbench, "Program.cs")),
            "手工入口 Program.cs 已迁入 App.xaml / App.xaml.cs，不应残留（会与生成的 Main 冲突）。");
    }

    /// <summary>匹配 sln 中 <c>Project(...) = "名称", "相对路径", "{GUID}"</c> 的相对路径。</summary>
    private static readonly Regex ProjectPathPattern = new(
        "Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"(?<path>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>从测试输出目录向上定位包含 ScriptEditor.sln 的模块根目录。</summary>
    /// <returns>模块根目录的绝对路径。</returns>
    private static string LocateSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScriptEditor.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("未能从测试输出目录向上定位 ScriptEditor.sln。");
    }

    /// <summary>读取模块下指定项目的 csproj 文档。</summary>
    /// <param name="relativeSegments">相对于模块根目录的路径片段。</param>
    /// <returns>解析后的 csproj 文档。</returns>
    private static XDocument LoadCsproj(params string[] relativeSegments)
        => XDocument.Parse(File.ReadAllText(Path.Combine([SolutionDirectory, .. relativeSegments])));

    /// <summary>返回 csproj 声明的项目引用路径（统一为正斜杠）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>项目引用路径数组。</returns>
    private static string[] ProjectReferences(XDocument csproj)
        => csproj.Descendants("ProjectReference")
            .Select(element => ((string?)element.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .ToArray();

    /// <summary>取引用路径的文件名部分（兼容正斜杠分隔）。</summary>
    /// <param name="reference">项目引用路径。</param>
    /// <returns>文件名（如 <c>ScriptEditor.Core.csproj</c>）。</returns>
    private static string FileName(string reference)
        => reference[(reference.LastIndexOf('/') + 1)..];

    /// <summary>返回 csproj 的程序集名（缺省时取项目文件名）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>程序集名。</returns>
    private static string AssemblyName(XDocument csproj)
        => csproj.Descendants("AssemblyName").FirstOrDefault()?.Value.Trim() ?? string.Empty;

    /// <summary>返回 csproj 的输出类型（默认 <c>Library</c>）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>输出类型字符串，如 <c>Library</c> / <c>WinExe</c>。</returns>
    private static string OutputType(XDocument csproj)
        => csproj.Descendants("OutputType").FirstOrDefault()?.Value.Trim() ?? "Library";

    /// <summary>返回 csproj 的目标框架（默认取单个 TargetFramework 元素）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>目标框架字符串。</returns>
    private static string TargetFramework(XDocument csproj)
        => csproj.Descendants("TargetFramework").FirstOrDefault()?.Value.Trim() ?? string.Empty;

    /// <summary>判断 csproj 是否显式启用 WPF（<c>&lt;UseWPF&gt;true&lt;/UseWPF&gt;</c>）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>启用 WPF 时返回 true。</returns>
    private static bool HasUseWpf(XDocument csproj)
        => csproj.Descendants("UseWPF")
            .Any(element => string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
}