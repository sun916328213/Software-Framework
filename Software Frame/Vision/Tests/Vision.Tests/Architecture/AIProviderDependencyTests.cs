// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AIProviderDependencyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、System.Text.RegularExpressions、Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P6 验收守卫 —— 三家 Provider 边界（计划 §P6 / §13 检查表）：
//   ① 三家构造都不碰 Runtime（Workbench 切换只构造不加载）；
//   ② 每家只认 Vision.Core + 所需 Extension 层；Invi 直连轻量 Core，互不引用；
//   ③ 源码无迁移源/兄弟命名空间 token。
// 口径沿用既有 Architecture 守卫：注释先剥、断言配防假绿下限。
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P6 三家 AI Provider 边界守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class AIProviderDependencyTests
{
    private static readonly string[] ProviderProjects =
    [
        "Vision.AI.Halcon",
        "Vision.AI.Aiditron",
        "Vision.AI.Invi",
    ];

    // ── 1. 构造不碰 Runtime ─────────────────────────────────────────

    [Fact]
    public void AllProviders_ConstructWithoutRuntime()
    {
        // 缺 DLL / Runtime / License 时也必须构造成功；加载失败在 OpenSession 暴露。
        _ = new Vision.AI.Halcon.HalconAIProvider();
        _ = new Vision.AI.Aiditron.AiditronProvider();
        _ = new Vision.AI.Invi.InviProvider();
    }

    // ── 2. csproj：只认 Core + Extension ────────────────────────────

    [Fact]
    public void ProviderProjects_ReferenceOnlyCoreAndExtension()
    {
        foreach (var project in ProviderProjects)
        {
            var csproj = VisionLayout.ProjectFile(VisionDirectory, project);
            Assert.True(File.Exists(csproj), $"工程不存在：{csproj}");
            var text = Regex.Replace(File.ReadAllText(csproj), "<!--.*?-->", " ", RegexOptions.Singleline);

            var refs = Regex
                .Matches(text, "<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")
                .Select(m => m.Groups["path"].Value)
                .ToArray();
            Assert.True(refs.Length > 0, $"{project} 一个工程引用都没解析到，正则疑似失效。");
            foreach (var reference in refs)
            {
                var file = Path.GetFileNameWithoutExtension(reference);
                var extension = project == "Vision.AI.Invi" ? "Extension.Core" : "Extension";
                Assert.True(
                    file == "Vision.Core" || file == extension,
                    $"{project} 引用了非允许工程：{reference}（只允许 Vision.Core + {extension}）。");
            }

            var packages = Regex
                .Matches(text, "<PackageReference\\s+Include=\"(?<name>[^\"]+)\"")
                .Select(m => m.Groups["name"].Value)
                .ToArray();
            Assert.DoesNotContain(packages, name =>
                name.StartsWith("MVTec", StringComparison.OrdinalIgnoreCase));
            Assert.False(
                Regex.IsMatch(text, "<Reference\\s+Include=\"halcondotnet", RegexOptions.IgnoreCase),
                $"{project} 存在 halcondotnet 直接引用（必须经 Extension）。");
        }
    }

    // ── 3. 源码：无迁移源/兄弟 token ────────────────────────────────

    [Fact]
    public void ProviderSources_HaveNoLegacyOrSiblingTokens()
    {
        var offenders = new List<string>();
        var total = 0;
        foreach (var project in ProviderProjects)
        {
            var files = EnumerateSourceFiles(VisionLayout.ProjectDirectory(VisionDirectory, project)).ToArray();
            total += files.Length;
            var siblings = ProviderProjects
                .Where(p => p != project)
                .ToArray();
            foreach (var file in files)
            {
                var code = StripComments(File.ReadAllText(file));
                var relative = Path.GetRelativePath(VisionDirectory, file).Replace('\\', '/');
                if (code.Contains("DPLibrary_Halcon", StringComparison.Ordinal))
                    offenders.Add($"{relative} -> DPLibrary_Halcon");
                foreach (var sibling in siblings)
                {
                    if (code.Contains(sibling, StringComparison.Ordinal))
                        offenders.Add($"{relative} -> {sibling}");
                }
            }
        }

        Assert.True(total >= 15, $"三家只扫到 {total} 个 .cs，扫描疑似失效。");
        Assert.Empty(offenders);
    }

    // ── 工具 ─────────────────────────────────────────────────────────

    private static string VisionDirectory => Path.Combine(RepoRoot, "Vision");

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DataManager", "DataManager.Core", "DataManager.Core.csproj")))
                dir = dir.Parent;
            return dir?.FullName
                ?? throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 向上找不到仓库根（DataManager/DataManager.Core/DataManager.Core.csproj）。");
        }
    }

    /// <summary>
    /// 测试相关入口 EnumerateSourceFiles；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Push、Pop、GetDirectories、Contains、GetFileName；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "obj", "bin", ".vs", ".git" };
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.GetDirectories(dir))
            {
                if (!skipDirs.Contains(Path.GetFileName(sub)))
                    stack.Push(sub);
            }

            foreach (var file in Directory.GetFiles(dir, "*.cs"))
                yield return file;
        }
    }

    /// <summary>
    /// 测试相关入口 StripComments；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Replace；具体条件和先后次序见方法体。
    /// </summary>
    private static string StripComments(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"(^|\s)//.*$", " ", RegexOptions.Multiline);
        return code;
    }
}
