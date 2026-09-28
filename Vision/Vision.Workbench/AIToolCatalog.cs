// ─────────────────────────────────────────────────────────────────────────────
// P5 —— Workbench 侧统一 AI Tool 注册表（与 P3/P4 Catalog 对等）。
//
// 只做“可实例化 + 可 Validate”的注册；Provider 由 P6 三家实现注入，
// P9 做 AI Provider 选择页（HALCON / Aiditron / Invi）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Tools.AI.AnomalyDetection;
using Vision.Tools.AI.Classification;
using Vision.Tools.AI.Detection;
using Vision.Tools.AI.Segmentation;

namespace Vision.Workbench;

/// <summary>
/// P5 统一 AI Tool 注册表。
/// </summary>
public static class AIToolCatalog
{
    /// <summary>P5 交付的全部 Tool（名 → 工厂）。</summary>
    public static IReadOnlyDictionary<string, Func<object>> Tools { get; } =
        new Dictionary<string, Func<object>>(StringComparer.Ordinal)
        {
            ["Classification"] = () => new ClassificationTool(),
            ["Detection"] = () => new DetectionTool(),
            ["Segmentation"] = () => new SegmentationTool(),
            ["AnomalyDetection"] = () => new AnomalyDetectionTool(),
        };

    /// <summary>全部 Tool 名（Tool Browser 列表用）。</summary>
    public static IReadOnlyList<string> ToolNames => Tools.Keys.ToArray();

    /// <summary>按名创建 Tool；未知名抛 <see cref="KeyNotFoundException"/>。</summary>
    public static object Create(string name) => Tools[name]();
}
