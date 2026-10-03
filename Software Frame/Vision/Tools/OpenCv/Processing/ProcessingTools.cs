// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ThresholdParameters、ThresholdResult、ThresholdTool、MorphologyOperation、MorphologyParameters、MorphologyResult、MorphologyTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：OpenCvSharp、Vision.Core.Imaging、Vision.Core.Results、Vision.Tools.OpenCv.Internal。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P4 —— ThresholdTool / MorphologyTool（Processing）。
// 对标 HALCON threshold / gray_opening 等灰度预处理，但不追求 1:1（计划 §1）。
//
// 标准化内容（计划 §7）：
//   · Parameters：阈值区间 / 形态学算子+核+迭代（显式建模）；
//   · Result：白像素数 + 前景占比 + 输出图（VisionImage，可链式喂给 Blob）；
//   · Validate：区间/核合法性；
//   · Run：InRange / MorphologyEx；
//   · 输出图用 Core VisionImage 承载，Mat 不出 Internal。
// ─────────────────────────────────────────────────────────────────────────────

using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Internal;

namespace Vision.Tools.OpenCv.Processing;

/// <summary>灰度阈值参数（与 HALCON 侧 BlobTool 的 MinGray/MaxGray 同语义，便于跨后端对比）。</summary>
public sealed class ThresholdParameters
{
    /// <summary>最小灰度（0~255，含）。</summary>
    public int MinGray { get; set; }

    /// <summary>最大灰度（0~255，含）。</summary>
    public int MaxGray { get; set; } = 255;
}

/// <summary>阈值分割结果。</summary>
public sealed class ThresholdResult : ToolResult
{
    /// <summary>前景（白色）像素数。</summary>
    public int WhitePixelCount { get; set; }

    /// <summary>前景占比（0~1）。</summary>
    public double ForegroundRatio { get; set; }

    /// <summary>二值输出图（Mono8，0/255；可直接喂给 Blob/Morphology）。</summary>
    public VisionImage? OutputImage { get; set; }
}

/// <summary>
/// 灰度区间阈值工具（Processing）：InRange。
/// </summary>
public sealed class ThresholdTool : OpenCvToolBase<ThresholdParameters, ThresholdResult>
{
    /// <inheritdoc/>
    public override string Name => "Threshold";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(ThresholdParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.MinGray < 0 || parameters.MinGray > 255)
            result.AddError("THRESHOLD_RANGE", $"最小灰度 {parameters.MinGray} 超出 0~255。", nameof(ThresholdParameters.MinGray));
        if (parameters.MaxGray < 0 || parameters.MaxGray > 255)
            result.AddError("THRESHOLD_RANGE", $"最大灰度 {parameters.MaxGray} 超出 0~255。", nameof(ThresholdParameters.MaxGray));
        if (parameters.MinGray > parameters.MaxGray)
            result.AddError("THRESHOLD_RANGE", $"最小灰度 {parameters.MinGray} 大于最大灰度 {parameters.MaxGray}。", nameof(ThresholdParameters.MinGray));
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 FromVisionImage、InRange、CountNonZero、ToVisionImage；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ThresholdResult RunCore(VisionImage image, ThresholdParameters parameters)
    {
        using var src = MatConverter.FromVisionImage(image);
        using var binary = new Mat();
        Cv2.InRange(src, new Scalar(parameters.MinGray), new Scalar(parameters.MaxGray), binary);
        var count = Cv2.CountNonZero(binary);
        return new ThresholdResult
        {
            WhitePixelCount = count,
            ForegroundRatio = (double)count / (image.Width * image.Height),
            OutputImage = MatConverter.ToVisionImage(binary, source: "Threshold"),
        };
    }
}

/// <summary>形态学算子。</summary>
public enum MorphologyOperation
{
    /// <summary>腐蚀。</summary>
    Erode,
    /// <summary>膨胀。</summary>
    Dilate,
    /// <summary>开运算（先腐蚀后膨胀）。</summary>
    Open,
    /// <summary>闭运算（先膨胀后腐蚀）。</summary>
    Close,
}

/// <summary>形态学参数。</summary>
public sealed class MorphologyParameters
{
    /// <summary>算子。</summary>
    public MorphologyOperation Operation { get; set; } = MorphologyOperation.Open;

    /// <summary>结构元边长（奇数，≥1）。</summary>
    public int KernelSize { get; set; } = 3;

    /// <summary>迭代次数（≥1）。</summary>
    public int Iterations { get; set; } = 1;
}

/// <summary>形态学结果。</summary>
public sealed class MorphologyResult : ToolResult
{
    /// <summary>处理后前景像素数。</summary>
    public int WhitePixelCount { get; set; }

    /// <summary>前景占比（0~1）。</summary>
    public double ForegroundRatio { get; set; }

    /// <summary>处理后输出图（Mono8）。</summary>
    public VisionImage? OutputImage { get; set; }
}

/// <summary>
/// 形态学工具（Processing）：Erode / Dilate / Open / Close。
/// </summary>
public sealed class MorphologyTool : OpenCvToolBase<MorphologyParameters, MorphologyResult>
{
    /// <inheritdoc/>
    public override string Name => "Morphology";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(MorphologyParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.KernelSize < 1 || parameters.KernelSize % 2 == 0)
            result.AddError("KERNEL_SIZE", $"结构元边长 {parameters.KernelSize} 须为正奇数。", nameof(MorphologyParameters.KernelSize));
        if (parameters.Iterations < 1)
            result.AddError("ITERATIONS", $"迭代次数 {parameters.Iterations} 须 ≥1。", nameof(MorphologyParameters.Iterations));
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 FromVisionImage、GetStructuringElement、Erode、Dilate、MorphologyEx；具体条件和先后次序见方法体。
    /// </remarks>
    protected override MorphologyResult RunCore(VisionImage image, MorphologyParameters parameters)
    {
        using var src = MatConverter.FromVisionImage(image);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(parameters.KernelSize, parameters.KernelSize));
        using var dst = new Mat();
        switch (parameters.Operation)
        {
            case MorphologyOperation.Erode:
                Cv2.Erode(src, dst, kernel, iterations: parameters.Iterations);
                break;
            case MorphologyOperation.Dilate:
                Cv2.Dilate(src, dst, kernel, iterations: parameters.Iterations);
                break;
            case MorphologyOperation.Open:
                Cv2.MorphologyEx(src, dst, MorphTypes.Open, kernel, iterations: parameters.Iterations);
                break;
            default:
                Cv2.MorphologyEx(src, dst, MorphTypes.Close, kernel, iterations: parameters.Iterations);
                break;
        }

        var count = Cv2.CountNonZero(dst);
        return new MorphologyResult
        {
            WhitePixelCount = count,
            ForegroundRatio = (double)count / (image.Width * image.Height),
            OutputImage = MatConverter.ToVisionImage(dst, source: "Morphology"),
        };
    }
}
