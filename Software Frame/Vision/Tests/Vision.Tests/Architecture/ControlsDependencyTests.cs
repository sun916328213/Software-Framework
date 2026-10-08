// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ControlsDependencyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.RegularExpressions、Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P8 验收守卫 —— Controls 分层（计划 §P8 / §13 检查表「Controls 是否没有被
// Tool 项目反向依赖」+「HALCON 特有显示先以 namespace 组织」）：
//   ① Core / Tools.* / AI.* 一律不得引用 Vision.Controls；
//   ② 新 UI 逻辑（ViewModels/、Presentation/、View/）不得出现视觉 SDK /
//      native / 迁移源 token（HALCON 显示只许住在 Ctr_Halcon/）。
// 口径沿用既有守卫：注释先剥、断言配防假绿下限.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.RegularExpressions;
using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P8 Controls 分层守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class ControlsDependencyTests
{
    private static readonly string[] ToolLayerProjects =
    [
        "Vision.Core",
        "Vision.Tools.Halcon",
        "Vision.Tools.OpenCv",
        "Vision.Tools.AI",
        "Vision.AI.Halcon",
        "Vision.AI.Aiditron",
        "Vision.AI.Invi",
    ];

    private static readonly string[] ForbiddenTokens =
    [
        "HalconDotNet",
        "HObject",
        "HTuple",
        "OpenCvSharp",
        "MVTec",
        "DPLibrary_Halcon",
        "DllImport",
    ];

    // ── 1. Tool 层不得反向引用 Controls ─────────────────────────────

    [Fact]
    public void ToolLayerProjects_DoNotReferenceControls()
    {
        var offenders = new List<string>();
        var scanned = 0;
        foreach (var project in ToolLayerProjects)
        {
            var csproj = VisionLayout.ProjectFile(VisionDirectory, project);
            Assert.True(File.Exists(csproj), $"工程不存在：{csproj}");
            scanned++;
            var text = Regex.Replace(File.ReadAllText(csproj), "<!--.*?-->", " ", RegexOptions.Singleline);
            var refs = Regex
                .Matches(text, "<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")
                .Select(m => m.Groups["path"].Value);
            foreach (var reference in refs)
            {
                if (reference.Contains("Vision.Controls", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{project} -> {reference}");
            }
        }

        Assert.True(scanned == ToolLayerProjects.Length, "扫描集合疑似被改。");
        Assert.Empty(offenders);
    }

    // ── 2. 新 UI 逻辑无 SDK/nativie/迁移源 token ─────────────────────

    [Fact]
    public void NewUiLogic_ContainsNoSdkOrLegacyTokens()
    {
        var roots = new[] { "ViewModels", "Presentation", "View" };
        var offenders = new List<string>();
        var total = 0;
        foreach (var root in roots)
        {
            var dir = Path.Combine(VisionDirectory, "Vision.Controls", root);
            Assert.True(Directory.Exists(dir), $"目录不存在：{dir}");
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                total++;
                var code = StripComments(File.ReadAllText(file));
                var relative = Path.GetRelativePath(VisionDirectory, file).Replace('\\', '/');
                foreach (var token in ForbiddenTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                        offenders.Add($"{relative} -> {token}");
                }
            }
        }

        Assert.True(total >= 6, $"只扫到 {total} 个 .cs，扫描疑似失效。");
        Assert.Empty(offenders);
    }

    // ── 3. 非 HALCON 命名空间基线干净 ───────────────────────────────

    [Fact]
    public void NonHalconNamespaces_ContainNoSdkTokens()
    {
        // Ctr_Halcon/ 是 HALCON 特有显示的合法居所；之外（Converter/、Ctr_Base/、
        // Properties/、ViewModels/、Presentation/、View/）不得出现 SDK token。
        var offenders = new List<string>();
        var total = 0;
        foreach (var file in Directory.GetFiles(
                     Path.Combine(VisionDirectory, "Vision.Controls"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Path.Combine(VisionDirectory, "Vision.Controls"), file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith(".vs/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Ctr_Halcon/", StringComparison.OrdinalIgnoreCase))
                continue;
            total++;
            var code = StripComments(File.ReadAllText(file));
            foreach (var token in ForbiddenTokens)
            {
                if (code.Contains(token, StringComparison.Ordinal))
                    offenders.Add($"{relative} -> {token}");
            }
        }

        Assert.True(total >= 10, $"只扫到 {total} 个 .cs，扫描疑似失效。");
        Assert.Empty(offenders);
    }

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
