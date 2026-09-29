// ─────────────────────────────────────────────────────────────────────────────
// P4 验收守卫 —— 依赖方向（计划 §P4 / §13 检查表）：
//   ① 旧 DPLibrary_Halcon_F48 不再直接引用 OpenCvSharp —— **已随迁移源删除而作废**
//      （2026-09-29 P9 收口；「迁移源已退场」由 RetirementAuditTests /
//      SolutionStructureTests 守卫，此处不再重复断言不存在的工程）。
//   ② Vision.Tools.OpenCv 可独立部署：只认 Vision.Core + OpenCvSharp，
//      不碰 HALCON（HalconDotNet / MVTec）与迁移源。
//
// 口径沿用 SolutionStructureTests / CorePurityTests：
//   · csproj 先剥 XML 注释再匹配（本仓注释里大量保留历史说明）；
//   · 源码先剥 // 与 /* */ 注释再扫 token（注释提及不算依赖）；
//   · 每条断言配防假绿下限。
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.RegularExpressions;
using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P4 依赖方向守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class OpenCvDependencyTests
{
    /// <summary>Vision.Tools.OpenCv 源码文件数下限（当前 ~8），扫描失效判据。</summary>
    private const int MinimumOpenCvSourceFiles = 6;

    // ── 1. OpenCV 后端独立：只认 Core + OpenCvSharp ───────────────────

    [Fact]
    public void OpenCvProject_ReferencesOnlyCore_AndOpenCvSharp()
    {
        var csproj = VisionLayout.ProjectFile(VisionDirectory, "Vision.Tools.OpenCv");
        Assert.True(File.Exists(csproj), $"OpenCV 后端工程不存在：{csproj}");
        var text = Regex.Replace(File.ReadAllText(csproj), "<!--.*?-->", " ", RegexOptions.Singleline);

        var projects = Regex
            .Matches(text, "<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")
            .Select(m => m.Groups["path"].Value)
            .ToArray();
        // 防假绿 + 独立性：恰好只引用 Vision.Core。
        Assert.Single(projects);
        Assert.Contains("Vision.Core", projects[0], StringComparison.OrdinalIgnoreCase);

        var packages = Regex
            .Matches(text, "<PackageReference\\s+Include=\"(?<name>[^\"]+)\"")
            .Select(m => m.Groups["name"].Value)
            .ToArray();
        Assert.Contains(packages, name => string.Equals(name, "OpenCvSharp4", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packages, name =>
            name.StartsWith("MVTec", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Halcon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OpenCvSources_ContainNoHalconOrLegacyTokens()
    {
        var files = EnumerateSourceFiles(
            VisionLayout.ProjectDirectory(VisionDirectory, "Vision.Tools.OpenCv")).ToArray();
        Assert.True(files.Length >= MinimumOpenCvSourceFiles, $"只扫到 {files.Length} 个 .cs，扫描疑似失效。");

        string[] forbidden = ["HalconDotNet", "MVTec", "DPLibrary_Halcon", "HObject", "HTuple"];
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var code = StripComments(File.ReadAllText(file));
            var relative = Path.GetRelativePath(VisionDirectory, file).Replace('\\', '/');
            foreach (var token in forbidden)
            {
                if (code.Contains(token, StringComparison.Ordinal))
                    offenders.Add($"{relative} -> {token}");
            }
        }

        Assert.Empty(offenders);
    }

    // ── 工具 ─────────────────────────────────────────────────────────

    private static string VisionDirectory => Path.Combine(RepoRoot, "Vision");

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Frame.sln")))
                dir = dir.Parent;
            return dir?.FullName
                ?? throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 向上找不到仓库根（Frame.sln）。");
        }
    }

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
    /// 剥 // 与 /* */ 注释（保留字符串；与 CorePurityTests 同口径的简化版 ——
    /// 本守卫只扫 OpenCvSharp/HalconDotNet 等 SDK token，字符串里出现即视为真实引用）。
    /// </summary>
    private static string StripComments(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"(^|\s)//.*$", " ", RegexOptions.Multiline);
        return code;
    }
}
