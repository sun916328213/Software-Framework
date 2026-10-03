// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：OpenCvToolCatalog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.OpenCv.Inspection、Vision.Tools.OpenCv.Matching、Vision.Tools.OpenCv.Measurement、Vision.Tools.OpenCv.Processing。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P4 —— Workbench 侧 P4 Tool 注册表（与 P3 HalconToolCatalog 对等）。
//
// 只做“可实例化 + 可 Validate”的注册（无相机也跑），完整 Tool Browser /
// HALCON vs OpenCV Compare 是 P9 范围，这里预留装配点。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Matching;
using Vision.Tools.OpenCv.Measurement;
using Vision.Tools.OpenCv.Processing;

namespace Vision.Workbench;

/// <summary>
/// P4 标准化 OpenCV Tool 注册表。
/// </summary>
public static class OpenCvToolCatalog
{
    /// <summary>P4 交付的全部标准化 Tool（名 → 工厂）。</summary>
    public static IReadOnlyDictionary<string, Func<object>> Tools { get; } =
        new Dictionary<string, Func<object>>(StringComparer.Ordinal)
        {
            ["Threshold（Processing)"] = () => new ThresholdTool(),
            ["Morphology（Processing)"] = () => new MorphologyTool(),
            ["Blob（Inspection)"] = () => new BlobTool(),
            ["TemplateMatch（Matching)"] = () => new TemplateMatchTool(),
            ["FindLine（Measurement)"] = () => new FindLineTool(),
            ["FindCircle（Measurement)"] = () => new FindCircleTool(),
        };

    /// <summary>全部 Tool 名（Tool Browser 列表用）。</summary>
    public static IReadOnlyList<string> ToolNames => Tools.Keys.ToArray();

    /// <summary>按名创建 Tool；未知名抛 <see cref="KeyNotFoundException"/>。</summary>
    public static object Create(string name) => Tools[name]();
}
