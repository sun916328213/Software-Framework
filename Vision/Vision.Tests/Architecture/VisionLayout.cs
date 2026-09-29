// ─────────────────────────────────────────────────────────────────────────────
// 物理分层单一真源（2026-09-29）。
//
// Vision/ 下的工程按层分组：Core / Controls / Workbench / Tests 留在 Vision/ 根，
// Runtime 层拆成两组目录 ——
//   Tools/ 视觉工具：Halcon、OpenCv、AI
//   AI/    厂商 AI Provider：Halcon、Aiditron、Invi
//
// ⛔ 工程名、命名空间、AssemblyName 一律不变，变的只有目录。
//    所以任何一个 `using Vision.Tools.Halcon.*` 都不会因为这次分组而失效。
//
// 所有守卫都从这里取路径：搬目录时只改这一个文件，
// 不必在 6 个守卫里各维护一份「名字 → 目录」的映射。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Tests.Architecture;

/// <summary>Vision 模块的物理布局（工程名 → 相对 <c>Vision/</c> 的目录，'/' 分隔）。</summary>
internal static class VisionLayout
{
    /// <summary>
    /// P1 交付的目标骨架 + 2026-09-29 的分组目录。
    /// 骨架新增/退场 ⇒ 守卫红，提醒同步更新这张表。
    /// </summary>
    internal static readonly (string Name, string Directory)[] Projects =
    [
        ("Vision.Core", "Vision.Core"),                       // ← DPLibrary_Base_F48（P1 改名）
        ("Vision.Tools.Halcon", "Tools/Halcon"),              // ← P1 新建空壳；2026-09-29 归入 Tools/
        ("Vision.Tools.OpenCv", "Tools/OpenCv"),              // ← P1 新建空壳；2026-09-29 归入 Tools/
        ("Vision.Tools.AI", "Tools/AI"),                     // ← AI 工具层；与其他 Vision.Tools.* 同组
        ("Vision.AI.Halcon", "AI/Halcon"),                    // ← P1 新建空壳；2026-09-29 归入 AI/
        ("Vision.AI.Aiditron", "AI/Aiditron"),                // ← P1 新建空壳；2026-09-29 归入 AI/
        ("Vision.AI.Invi", "AI/Invi"),                        // ← P1 新建空壳；2026-09-29 归入 AI/
        ("Vision.Controls", "Vision.Controls"),               // ← Ctr_WPF_F48（P1 改名；P2 收口为 Vision.Controls）
        ("Vision.Workbench", "Vision.Workbench"),             // ← VisionDemo（P1 改名）
        ("Vision.Tests", "Vision.Tests"),                     // Core/Tools/AI 纯逻辑测试线（无 WPF）
        ("Vision.Workbench.Tests", "Vision.Workbench.Tests"), // 宿主集成测试线（UseWPF + STA）
    ];

    /// <summary>全部工程名。</summary>
    internal static string[] ProjectNames => Projects.Select(entry => entry.Name).ToArray();

    /// <summary>
    /// 按工程名取相对 <c>Vision/</c> 的目录。
    /// 未登记的工程名直接抛 —— 守卫里写错名字时立刻暴露，而不是静默比对失败。
    /// </summary>
    internal static string DirectoryOf(string projectName)
    {
        foreach (var entry in Projects)
        {
            if (string.Equals(entry.Name, projectName, StringComparison.Ordinal))
                return entry.Directory;
        }

        throw new ArgumentOutOfRangeException(
            nameof(projectName), projectName, "未在 VisionLayout.Projects 登记的工程名。");
    }

    /// <summary>工程目录的绝对路径。</summary>
    internal static string ProjectDirectory(string visionRoot, string projectName) =>
        Path.Combine(visionRoot, DirectoryOf(projectName));

    /// <summary>工程文件的绝对路径。</summary>
    internal static string ProjectFile(string visionRoot, string projectName) =>
        Path.Combine(visionRoot, DirectoryOf(projectName), projectName + ".csproj");
}
