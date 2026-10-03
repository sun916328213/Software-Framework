// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：HalconToolCatalog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.Halcon.Acquisition、Vision.Tools.Halcon.Calibration、Vision.Tools.Halcon.Coordinate、Vision.Tools.Halcon.Identification、Vision.Tools.Halcon.Inspection、Vision.Tools.Halcon.Matching。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P3 —— Workbench 侧 P3 Tool 注册表（计划 §11「新 Tool 完成的标准流程：
// 开发 -> 注册 Workbench -> 参数/ROI 验证 -> 结果验证」的第一步：注册）。
//
// 本文件只做“可实例化 + 可 Validate”的注册（无 HALCON Runtime 也能跑），
// 完整的 Tool Browser / 参数编辑 / Result Viewer 是 P9 的范围。
// 旧 MainWindow 页签（绑定 DPLibrary_Halcon_F48 的 HTool_*）已于 2026-09-29 随
// 迁移源退场删除（见 MainWindow.xaml），Tool Browser 现为主窗口唯一入口。
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
