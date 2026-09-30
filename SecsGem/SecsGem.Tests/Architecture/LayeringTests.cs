using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SecsGem.Tests.Architecture;

/// <summary>
/// P7 SecsGem 边界守卫：钉住 Core ← Runtime ← Controls ← Workbench 的分层方向，
/// 以及「逻辑测试线无 WPF 且直引 Core + Runtime」的测试拆分规则，防止架构回退。
/// </summary>
public sealed class LayeringTests
{
    /// <summary>Secs.sln 所在目录（即 SecsGem 模块根）。</summary>
    private static readonly string SolutionDirectory = LocateSolutionDirectory();

    /// <summary>Core 是纯契约/模型层：不引用任何工程，也不启用 WPF。</summary>
    [Fact]
    public void Core_project_has_no_project_references()
    {
        var csproj = LoadCsproj("SecsGem.Core", "SecsGem.Core.csproj");

        Assert.False(HasUseWpf(csproj), "SecsGem.Core 是纯契约层，不应启用 WPF。");
        Assert.Equal("net9.0", TargetFramework(csproj));
        Assert.Empty(ProjectReferences(csproj));
    }

    /// <summary>
    /// Runtime 只依赖 Core，并复用 DataManager（配置/日志）、Extension（通用能力）与
    /// Secs4Net 协议栈；白名单之外的引用一律视为越界，且禁止反向依赖 UI。
    /// </summary>
    [Fact]
    public void Runtime_project_references_are_whitelisted()
    {
        var csproj = LoadCsproj("SecsGem.Runtime", "SecsGem.Runtime.csproj");

        Assert.False(HasUseWpf(csproj), "SecsGem.Runtime 是协议编排层，不应启用 WPF。");
        Assert.NotEqual("WinExe", OutputType(csproj));

        // 白名单必须精确匹配：既防漏（Core 丢失、协议栈被复制），也防越界新增依赖。
        var expected = new[]
        {
            "DataManager.Core.csproj",
            "DataManager.Runtime.csproj",
            "Extension.csproj",
            "Secs4Net.Json.csproj",
            "Secs4Net.Sml.csproj",
            "Secs4Net.csproj",
            "SecsGem.Core.csproj",
        };
        var actual = ProjectReferences(csproj).Select(FileName).OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Controls 是 WPF 表现层，只面向 SecsGem.Core 契约工作；引用 SecsGem.Runtime
    /// 会把协议栈与传输实现拖进 UI 程序集，是本模块最核心的不变量。
    /// </summary>
    [Fact]
    public void Controls_project_never_references_runtime()
    {
        var csproj = LoadCsproj("SecsGem.Controls", "SecsGem.Controls.csproj");

        Assert.True(HasUseWpf(csproj), "SecsGem.Controls 是 WPF 表现层，应启用 WPF。");

        var projects = ProjectReferences(csproj);
        // 下限断言防假绿：已知引用必须都在。
        Assert.Contains(projects, r => r.EndsWith("SecsGem.Core.csproj", StringComparison.Ordinal));

        // 核心不变量：UI 层不得直引 Runtime（协议栈只进宿主）。
        Assert.DoesNotContain(projects, r => r.Contains("SecsGem.Runtime"));
    }

    /// <summary>
    /// 逻辑测试线保持无界面：不启用 WPF、只直引 Core + Runtime（TFM 因 Runtime 依赖 windows
    /// 目标而取 windows7.0），不经 Controls / Workbench 等顶层聚合项目间接获得依赖。
    /// </summary>
    [Fact]
    public void Logic_tests_line_stays_wpf_free_and_directly_references_core_and_runtime()
    {
        var csproj = LoadCsproj("SecsGem.Tests", "SecsGem.Tests.csproj");

        Assert.False(HasUseWpf(csproj), "SecsGem.Tests 是逻辑测试线，不应启用 WPF。");
        Assert.Equal("net9.0-windows7.0", TargetFramework(csproj));

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.EndsWith("SecsGem.Core.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, r => r.EndsWith("SecsGem.Runtime.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(projects, r => r.Contains("SecsGem.Controls"));
        Assert.DoesNotContain(projects, r => r.Contains("SecsGem.Workbench"));
    }

    /// <summary>
    /// Workbench 是 Prism 宿主：必须有 App.xaml（ApplicationDefinition）作为唯一入口，
    /// 且不得残留手工装配的 Program.cs —— 它会与 App.xaml 生成的 Main 冲突。
    /// 宿主是唯一直接组合 Runtime 的生产项目。
    /// </summary>
    [Fact]
    public void Workbench_is_prism_application_with_app_xaml_as_sole_entry()
    {
        var workbench = Path.Combine(SolutionDirectory, "SecsGem.Workbench");

        Assert.True(File.Exists(Path.Combine(workbench, "App.xaml")), "SecsGem.Workbench 应提供 App.xaml。");
        Assert.True(File.Exists(Path.Combine(workbench, "App.xaml.cs")), "SecsGem.Workbench 应提供 App.xaml.cs。");
        Assert.False(
            File.Exists(Path.Combine(workbench, "Program.cs")),
            "手工入口 Program.cs 不应存在（会与 App.xaml 生成的 Main 冲突）。");

        var csproj = LoadCsproj("SecsGem.Workbench", "SecsGem.Workbench.csproj");
        Assert.Equal("WinExe", OutputType(csproj));

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.EndsWith("SecsGem.Controls.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, r => r.EndsWith("SecsGem.Runtime.csproj", StringComparison.Ordinal));
    }

    /// <summary>
    /// 解决方案登记的每个项目路径都必须真实存在。P7 把 SecsGem.Core.Tests / SecsGem.Runtime.Tests
    /// 合并为 SecsGem.Tests、新增 SecsGem.Workbench.Tests；此守卫防止登记与实际磁盘再次脱节。
    /// </summary>
    [Fact]
    public void Solution_project_paths_all_exist_on_disk()
    {
        var sln = File.ReadAllText(Path.Combine(SolutionDirectory, "Secs.sln"));
        var declared = ProjectPathPattern.Matches(sln)
            .Select(match => match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

        // 下限断言防假绿：两条测试线与生产工程都应被解析到。
        Assert.Contains(declared, p => p.EndsWith("SecsGem.Core.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("SecsGem.Runtime.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("SecsGem.Controls.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("SecsGem.Workbench.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("SecsGem.Tests.csproj", StringComparison.Ordinal));
        Assert.Contains(declared, p => p.EndsWith("SecsGem.Workbench.Tests.csproj", StringComparison.Ordinal));

        // 架构只认两条线（Xxx.Tests + Xxx.Workbench.Tests）：已合并的碎片工程不得再被登记。
        Assert.DoesNotContain(declared, p => p.EndsWith("SecsGem.Core.Tests.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(declared, p => p.EndsWith("SecsGem.Runtime.Tests.csproj", StringComparison.Ordinal));

        var missing = declared
            .Where(relative => !File.Exists(Path.Combine(SolutionDirectory, relative)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(missing);
    }

    /// <summary>匹配 sln 中 <c>Project(...) = "名称", "相对路径", "{GUID}"</c> 的相对路径。</summary>
    private static readonly Regex ProjectPathPattern = new(
        "Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"(?<path>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>从测试输出目录向上定位包含 Secs.sln 的模块根目录。</summary>
    /// <returns>模块根目录的绝对路径。</returns>
    private static string LocateSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Secs.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("未能从测试输出目录向上定位 Secs.sln。");
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
    /// <returns>文件名（如 <c>SecsGem.Core.csproj</c>）。</returns>
    private static string FileName(string reference)
        => reference[(reference.LastIndexOf('/') + 1)..];

    /// <summary>返回 csproj 的目标框架（默认取单个 TargetFramework 元素）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>目标框架字符串。</returns>
    private static string TargetFramework(XDocument csproj)
        => csproj.Descendants("TargetFramework").FirstOrDefault()?.Value.Trim() ?? string.Empty;

    /// <summary>返回 csproj 的输出类型（默认 <c>Library</c>）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>输出类型字符串，如 <c>Library</c> / <c>WinExe</c>。</returns>
    private static string OutputType(XDocument csproj)
        => csproj.Descendants("OutputType").FirstOrDefault()?.Value.Trim() ?? "Library";

    /// <summary>判断 csproj 是否显式启用 WPF（<c>&lt;UseWPF&gt;true&lt;/UseWPF&gt;</c>）。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <returns>启用 WPF 时返回 true。</returns>
    private static bool HasUseWpf(XDocument csproj)
        => csproj.Descendants("UseWPF")
            .Any(element => string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
}