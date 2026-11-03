using Vision.Controls;
using Vision.Core.Imaging;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>
/// 可在 UI 上展示与执行的 Tool（P3/P4/P5 新 Tool 的 UI 侧统一形状）。
/// </summary>
public interface IExecutableTool : Vision.Core.Tools.IImageTool
{
    Vision.Core.Presentation.ITemplateTool? TemplateTool => null;
    ICropRegionTool? CropRegionTool => null;
    Vision.Core.Roi.ISearchRegionTool? SearchRegionTool => null;
    Vision.Core.Presentation.ILineSearchFixtureTool? LineSearchFixtureTool => null;
    Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) => null;
    IReadOnlyList<NumericParameterDefinition> NumericInputs => Array.Empty<NumericParameterDefinition>();
    Func<ToolResult>? PrepareBackgroundRun(VisionImage? image, NumericParameterValue? input)
    {
        if (input != null) throw new NotSupportedException("此适配器未声明数值参数消费能力。");
        return PrepareBackgroundRun(image);
    }
    IReadOnlyList<LineParameterDefinition> LineInputs => Array.Empty<LineParameterDefinition>();
    Func<ToolResult> PrepareLineBackgroundRun(VisionImage? image, LineParameterValue input) =>
        throw new NotSupportedException("此适配器未声明线段消费能力。");
    IReadOnlyList<CircleParameterDefinition> CircleInputs => Array.Empty<CircleParameterDefinition>();
    Func<ToolResult> PrepareCircleBackgroundRun(VisionImage? image, CircleParameterValue input) =>
        throw new NotSupportedException("此适配器未声明圆消费能力。");
    IReadOnlyList<PointParameterDefinition> PointInputs => Array.Empty<PointParameterDefinition>();
    Func<ToolResult> PreparePointBackgroundRun(VisionImage? image, PointParameterValue input) =>
        throw new NotSupportedException("此适配器未声明点消费能力。");
    /// <summary>工具名称。</summary>
    new string Name { get; }

    /// <summary>运行前校验（纯逻辑，无需图像/Runtime）。</summary>
    new ValidationResult Validate();

    /// <summary>执行一次；失败转为失败结果，不抛（各 Tool 基类契约）。</summary>
    new ToolResult Run(VisionImage? image);

    /// <summary>上次执行耗时（毫秒）。</summary>
    new long LastElapsedMs { get; }

    // 保留旧接口成员身份；只实现旧接口的显式适配器也可通过 Core 执行面调用。
    string Vision.Core.Tools.IImageTool.Name => Name;
    ValidationResult Vision.Core.Tools.IImageTool.Validate() => Validate();
    ToolResult Vision.Core.Tools.IImageTool.Run(VisionImage? image) => Run(image);
    long Vision.Core.Tools.IImageTool.LastElapsedMs => LastElapsedMs;

    /// <summary>
    /// 确保可执行（AI 类 Tool 加载模型；传统 Tool 无操作）。
    /// 失败抛异常，调用方转状态文本（见 ToolCardViewModel）。
    /// </summary>
    void EnsureLoaded();
}
