// ─────────────────────────────────────────────────────────────────────────────
// P9 验收守卫 —— 退场审计（计划 §P9「完成 Vision.csproj 与旧 DPLibrary_*
// 退场审计和删除」+ 验收「旧项目不再承载新代码」）：
//   ① Legacy 的 Vision 工程已从 Vision.sln 除名且目录已删除；
//   ② DPLibrary_Halcon_F48 因模块外消费者保留，但仍钉住「迁移来源、不得新增功能」
//      标记（消费者集合冻结由 P1 MigrationSource_ConsumerSetIsFrozen 覆盖）。
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P9 退场审计守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class RetirementAuditTests
{
    [Fact]
    public void LegacyVisionProject_IsRemovedFromSolutionAndDisk()
    {
        var sln = Path.Combine(VisionDirectory, "Vision.sln");
        Assert.True(File.Exists(sln), $"解决方案不存在：{sln}");
        var text = File.ReadAllText(sln);
        Assert.DoesNotContain("\"Vision\", \"Vision\\Vision.csproj\"", text);
        Assert.False(Directory.Exists(Path.Combine(VisionDirectory, "Vision")), "Legacy 的 Vision 目录应已删除。");
        Assert.False(File.Exists(Path.Combine(VisionDirectory, "Vision", "Vision.csproj")), "Legacy 工程文件应已删除。");
    }

    [Fact]
    public void MigrationSource_RetainsNoNewCodeMarker()
    {
        // DPLibrary 因 FlowNodes / Packing Machine / WPFControl 的引用而保留；
        // 本断言钉住它的「只读迁移源」身份：标记注释还在 = 没人把它当新功能库改用途。
        var csproj = Path.Combine(VisionDirectory, "DPLibrary_Halcon_F48", "DPLibrary_Halcon_F48.csproj");
        Assert.True(File.Exists(csproj), $"迁移源工程不存在：{csproj}");
        var text = File.ReadAllText(csproj);
        Assert.Contains("不得再新增功能", text);
        Assert.Contains("P9", text);
    }

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
}
