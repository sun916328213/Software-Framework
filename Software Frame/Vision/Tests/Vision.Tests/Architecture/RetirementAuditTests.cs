// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RetirementAuditTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P9 验收守卫 —— 退场审计（计划 §P9「完成 Vision.csproj 与旧 DPLibrary_*
// 退场审计和删除」+ 验收「旧项目不再承载新代码」）：
//   ① Legacy 的 Vision 工程已从 Vision.sln 除名且目录已删除；
//   ② 迁移源 DPLibrary_Halcon_F48 已从 Vision.sln 除名且目录已删除
//      （2026-09-29 收口：原先因模块外消费者保留，现消费者侧引用已全部摘除）。
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P9 退场审计守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class RetirementAuditTests
{
    /// <summary>
    /// 测试相关入口 LegacyVisionProject_IsRemovedFromSolutionAndDisk；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、True、Exists、ReadAllText、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 MigrationSource_IsRemovedFromSolutionAndDisk；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、True、Exists、ReadAllText、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MigrationSource_IsRemovedFromSolutionAndDisk()
    {
        var sln = Path.Combine(VisionDirectory, "Vision.sln");
        Assert.True(File.Exists(sln), $"解决方案不存在：{sln}");
        var text = File.ReadAllText(sln);
        Assert.DoesNotContain("\"DPLibrary_Halcon_F48\"", text);
        Assert.False(
            Directory.Exists(Path.Combine(VisionDirectory, "DPLibrary_Halcon_F48")),
            "迁移源目录应已删除（P9 收口）。");
        Assert.False(
            File.Exists(Path.Combine(
                VisionDirectory, "DPLibrary_Halcon_F48", "DPLibrary_Halcon_F48.csproj")),
            "迁移源工程文件应已删除（P9 收口）。");
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
}
