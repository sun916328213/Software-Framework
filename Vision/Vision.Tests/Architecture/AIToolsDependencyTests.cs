// ─────────────────────────────────────────────────────────────────────────────
// P5 验收守卫 —— 统一 AI Tool 层边界（计划 §P5 / §13 检查表）：
//   ① Vision.Tools.AI 仅引用 Vision.Core（不引用三家 Provider 与任何厂商 SDK）；
//   ② 源码不出现品牌/厂商 token（Tool 通过 IAIProvider 工作，不判断品牌）；
//   ③ 四 Tool 公开成员无品牌概念。
//
// 口径沿用 CorePurityTests / OpenCvDependencyTests：注释先剥、断言配防假绿下限。
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.RegularExpressions;
using Vision.Core.AI;
using Vision.Tools.AI.AnomalyDetection;
using Vision.Tools.AI.Classification;
using Vision.Tools.AI.Detection;
using Vision.Tools.AI.Segmentation;
using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P5 统一 AI Tool 层守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class AIToolsDependencyTests
{
    private static readonly Type[] ToolTypes =
    [
        typeof(ClassificationTool),
        typeof(DetectionTool),
        typeof(SegmentationTool),
        typeof(AnomalyDetectionTool),
    ];

    /// <summary>Vision.Tools.AI 源码文件数下限（当前 6），扫描失效判据。</summary>
    private const int MinimumToolsAISourceFiles = 5;

    // ── 1. csproj：仅引 Vision.Core，无厂商包 ─────────────────────────

    [Fact]
    public void ToolsAIProject_ReferencesOnlyCore_AndNoVendorPackages()
    {
        var csproj = VisionLayout.ProjectFile(VisionDirectory, "Vision.Tools.AI");
        Assert.True(File.Exists(csproj), $"统一 AI Tool 工程不存在：{csproj}");
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
        Assert.DoesNotContain(packages, name =>
            name.StartsWith("MVTec", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Halcon", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("OpenCvSharp", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Aiditron", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Invi", StringComparison.OrdinalIgnoreCase));
    }

    // ── 2. 程序集引用：无 Provider / SDK ─────────────────────────────

    [Fact]
    public void ToolsAIAssembly_ReferencesNoProvidersOrVendorSdk()
    {
        var referenced = typeof(ClassificationTool).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();
        Assert.True(referenced.Length > 0, "一个引用都没解析到，反射疑似失效。");

        var offenders = referenced
            .Where(name =>
                name.StartsWith("Vision.AI.", StringComparison.Ordinal) ||
                name.StartsWith("HalconDotNet", StringComparison.Ordinal) ||
                name.StartsWith("MVTec", StringComparison.Ordinal) ||
                name.StartsWith("OpenCvSharp", StringComparison.Ordinal) ||
                name.Contains("Aiditron", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Invi", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Empty(offenders);
    }

    // ── 3. 源码：无品牌/厂商 token ───────────────────────────────────

    [Fact]
    public void ToolsAISources_ContainNoBrandOrSdkTokens()
    {
        var files = EnumerateSourceFiles(
            VisionLayout.ProjectDirectory(VisionDirectory, "Vision.Tools.AI")).ToArray();
        Assert.True(files.Length >= MinimumToolsAISourceFiles, $"只扫到 {files.Length} 个 .cs，扫描疑似失效。");

        string[] forbidden = ["HalconDotNet", "MVTec", "OpenCvSharp", "Aiditron", "Vision.AI.", "AIBrand", "HObject", "HTuple"];
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

    // ── 4. Tool 公开成员：无品牌概念 ─────────────────────────────────

    [Fact]
    public void Tools_HaveNoBrandMembers_AndFixedModelTypes()
    {
        Assert.True(ToolTypes.Length == 4, "Tool 登记不为 4，清单疑似被改。");
        var expectations = new Dictionary<Type, (AIModelType Type, AICapabilities Capability)>
        {
            [typeof(ClassificationTool)] = (AIModelType.Classification, AICapabilities.Classification),
            [typeof(DetectionTool)] = (AIModelType.Detection, AICapabilities.Detection),
            [typeof(SegmentationTool)] = (AIModelType.Segmentation, AICapabilities.Segmentation),
            [typeof(AnomalyDetectionTool)] = (AIModelType.AnomalyDetection, AICapabilities.AnomalyDetection),
        };

        foreach (var type in ToolTypes)
        {
            var brandMembers = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name.Contains("Brand", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Name)
                .ToArray();
            Assert.Empty(brandMembers);

            var instance = Activator.CreateInstance(type)!;
            var modelType = (AIModelType)type.GetProperty("ModelType")!.GetValue(instance)!;
            var capability = (AICapabilities)type.GetProperty("RequiredCapability")!.GetValue(instance)!;
            Assert.Equal(expectations[type].Type, modelType);
            Assert.Equal(expectations[type].Capability, capability);
        }
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

    private static string StripComments(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"(^|\s)//.*$", " ", RegexOptions.Multiline);
        return code;
    }
}
