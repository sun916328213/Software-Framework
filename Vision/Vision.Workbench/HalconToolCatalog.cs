// ─────────────────────────────────────────────────────────────────────────────
// P3 —— Workbench 侧 P3 Tool 注册表（计划 §11「新 Tool 完成的标准流程：
// 开发 -> 注册 Workbench -> 参数/ROI 验证 -> 结果验证」的第一步：注册）。
//
// 本文件只做“可实例化 + 可 Validate”的注册（无 HALCON Runtime 也能跑），
// 完整的 Tool Browser / 参数编辑 / Result Viewer 是 P9 的范围。
// 旧 MainWindow 页签（绑定 DPLibrary_Halcon_F48 的 HTool_*）保持不动，新旧并存到 P9。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Tools.Halcon.Acquisition;
using Vision.Tools.Halcon.Calibration;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Inspection;
using Vision.Tools.Halcon.Matching;
using Vision.Tools.Halcon.Measurement;

namespace Vision.Workbench;

/// <summary>
/// P3 标准化 HALCON Tool 注册表。
/// </summary>
public static class HalconToolCatalog
{
    /// <summary>P3 交付的全部标准化 Tool（名 → 工厂）。</summary>
    public static IReadOnlyDictionary<string, Func<object>> Tools { get; } =
        new Dictionary<string, Func<object>>(StringComparer.Ordinal)
        {
            ["Blob（Inspection)"] = () => new BlobTool(),
            ["ShapeMatch（Matching)"] = () => new ShapeMatchTool(),
            ["FindLine（Measurement)"] = () => new FindLineTool(),
            ["Barcode（Identification)"] = () => new BarcodeTool(),
            ["DataCode（Identification)"] = () => new DataCodeTool(),
            ["NPointsCalibration（Calibration)"] = () => new NPointsCalibrationTool(),
            ["Fixture（Coordinate)"] = () => new FixtureTool(),
            ["ImageFile（Acquisition)"] = () => new ImageFileTool(),
        };

    /// <summary>全部 Tool 名（Tool Browser 列表用）。</summary>
    public static IReadOnlyList<string> ToolNames => Tools.Keys.ToArray();

    /// <summary>按名创建 Tool；未知名抛 <see cref="KeyNotFoundException"/>。</summary>
    public static object Create(string name) => Tools[name]();
}
