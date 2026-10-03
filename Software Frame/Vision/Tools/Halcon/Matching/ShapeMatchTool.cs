// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ShapeMatchParameters、ShapeMatchItem、ShapeMatchResult、ShapeMatchTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P3 —— ShapeMatchTool（Matching）。
// 迁移源：DPLibrary_Halcon_F48/Tools/TemplateMatch/HTool_TemplateMatch.cs
//       （ShapeBasedMatching 分支）+ HTool_TemplateControl.cs（建模侧）。
//
// 标准化内容（计划 §7「需要训练或模型的 Tool 单独管理 Train / Load / Unload 生命周期」）：
//   · Parameters：角度/缩放搜索范围、最小分数、匹配个数、重叠度、贪婪度；
//   · Result：MatchItem 列表（中心 Point2D、角度度、缩放、分数；旧 HTemplateResult 的
//     Center/R/Scale/Score 语义原样保留，类型换成 Core）；
//   · Validate：分数/个数/重叠度/贪婪度区间检查 + 模型已训练检查（Warning/Run 时 Error）；
//   · Train：由模板图像+模板区域创建 scaled shape model（与旧建模一致）；
//   · Run：FindScaledShapeModel（算法与旧一致）；
//   · Dispose：释放 shape model 句柄（旧代码缺失的释放语义，P3 补齐）。
//
// ⛔ 类名表达能力（计划 §5.2）：ShapeMatchTool，不叫 HTool_TemplateMatch。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Tools.Halcon.Matching;

/// <summary>形状匹配运行参数。</summary>
public sealed class ShapeMatchParameters
{
    /// <summary>起始角度（度）。</summary>
    public double AngleStartDeg { get; set; }

    /// <summary>角度范围（度，必须 ≥ 0）。</summary>
    public double AngleExtentDeg { get; set; } = 360;

    /// <summary>最小缩放。</summary>
    public double ScaleMin { get; set; } = 1;

    /// <summary>最大缩放（必须 ≥ <see cref="ScaleMin"/>）。</summary>
    public double ScaleMax { get; set; } = 1;

    /// <summary>最小分数（0~1）。</summary>
    public double MinScore { get; set; } = 0.5;

    /// <summary>最大匹配个数（≥1）。</summary>
    public int MaxMatches { get; set; } = 1;

    /// <summary>最大重叠度（0~1）。</summary>
    public double MaxOverlap { get; set; }

    /// <summary>贪婪度（0~1）。</summary>
    public double Greediness { get; set; } = 0.9;
}

/// <summary>单个匹配。</summary>
public sealed class ShapeMatchItem
{
    /// <summary>中心（图像坐标：X=列，Y=行）。</summary>
    public Point2D Center { get; set; }

    /// <summary>角度（度）。</summary>
    public double AngleDeg { get; set; }

    /// <summary>缩放。</summary>
    public double Scale { get; set; }

    /// <summary>分数（0~1）。</summary>
    public double Score { get; set; }
}

/// <summary>形状匹配结果。</summary>
public sealed class ShapeMatchResult : ToolResult
{
    /// <summary>全部匹配（按分数降序由 HALCON 返回）。</summary>
    public List<ShapeMatchItem> Matches { get; set; } = [];
}

/// <summary>
/// 形状匹配工具（Matching）：scaled shape model 的训练与搜索。
/// </summary>
public sealed class ShapeMatchTool : Internal.HalconToolBase<ShapeMatchParameters, ShapeMatchResult>
{
    private HTuple? _modelId;
    private bool _disposed;

    /// <inheritdoc/>
    public override string Name => "ShapeMatch";

    /// <summary>模型是否已训练。</summary>
    public bool IsModelTrained => _modelId != null;

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(ShapeMatchParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.AngleExtentDeg < 0)
            result.AddError("ANGLE_EXTENT", $"角度范围 {parameters.AngleExtentDeg} 不能为负。", nameof(ShapeMatchParameters.AngleExtentDeg));
        if (parameters.ScaleMin <= 0)
            result.AddError("SCALE_MIN", $"最小缩放 {parameters.ScaleMin} 必须为正。", nameof(ShapeMatchParameters.ScaleMin));
        if (parameters.ScaleMax < parameters.ScaleMin)
            result.AddError("SCALE_RANGE", $"最大缩放 {parameters.ScaleMax} 小于最小缩放 {parameters.ScaleMin}。", nameof(ShapeMatchParameters.ScaleMax));
        if (parameters.MinScore < 0 || parameters.MinScore > 1)
            result.AddError("MIN_SCORE", $"最小分数 {parameters.MinScore} 超出 0~1。", nameof(ShapeMatchParameters.MinScore));
        if (parameters.MaxMatches < 1)
            result.AddError("MAX_MATCHES", $"最大匹配个数 {parameters.MaxMatches} 必须 ≥1。", nameof(ShapeMatchParameters.MaxMatches));
        if (parameters.MaxOverlap < 0 || parameters.MaxOverlap > 1)
            result.AddError("MAX_OVERLAP", $"最大重叠度 {parameters.MaxOverlap} 超出 0~1。", nameof(ShapeMatchParameters.MaxOverlap));
        if (parameters.Greediness < 0 || parameters.Greediness > 1)
            result.AddError("GREEDINESS", $"贪婪度 {parameters.Greediness} 超出 0~1。", nameof(ShapeMatchParameters.Greediness));
        if (!IsModelTrained)
            result.AddError("MODEL_NOT_TRAINED", "模型尚未训练，请先调用 Train。", "Model");
        return result;
    }

    /// <summary>
    /// 由模板图像 + 模板区域训练 scaled shape model（需要 Runtime）。
    /// 旧模板区域为空时按全图训练（与旧 HTool_TemplateControl 行为一致）。
    /// </summary>
    /// <param name="templateImage">模板图像（调用方保留所有权）。</param>
    /// <param name="templateRegion">模板区域（可为 null；调用方保留所有权）。</param>
    /// <param name="scaleMin">训练缩放下限（默认覆盖 Run 的搜索范围）。</param>
    /// <param name="scaleMax">训练缩放上限。</param>
    public void Train(HObject templateImage, HObject? templateRegion = null, double scaleMin = 0.5, double scaleMax = 2.5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(templateImage);

        HObject? reduced = null;
        HTuple? model = null;
        try
        {
            if (templateRegion != null)
                HOperatorSet.ReduceDomain(templateImage, templateRegion, out reduced);
            else
                reduced = templateImage.Clone();

            HOperatorSet.CreateScaledShapeModel(
                reduced, "auto", 0, Math.PI * 2, "auto",
                scaleMin, scaleMax, "auto",
                "auto", "ignore_local_polarity", "auto", "auto", out model);

            _modelId?.Dispose();
            _modelId = model;
            model = null;
        }
        finally
        {
            if (templateRegion != null)
                reduced?.Dispose();
            else if (!ReferenceEquals(reduced, templateImage))
                reduced?.Dispose();
            model?.Dispose();
        }
    }

    /// <summary>卸载模型（释放句柄，可重新 Train）。</summary>
    public void UnloadModel()
    {
        _modelId?.Dispose();
        _modelId = null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ThrowIfNull、FindScaledShapeModel、Add、Round；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ShapeMatchResult RunCore(HObject? image, ShapeMatchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_modelId == null)
            throw new InvalidOperationException("模型尚未训练，请先调用 Train。");

        var result = new ShapeMatchResult();
        HTuple? row = null;
        HTuple? column = null;
        HTuple? angle = null;
        HTuple? scale = null;
        HTuple? score = null;
        try
        {
            HOperatorSet.FindScaledShapeModel(
                image, _modelId,
                parameters.AngleStartDeg * Math.PI / 180.0,
                parameters.AngleExtentDeg * Math.PI / 180.0,
                parameters.ScaleMin, parameters.ScaleMax,
                parameters.MinScore, parameters.MaxMatches, parameters.MaxOverlap,
                "least_squares", 0, parameters.Greediness,
                out row, out column, out angle, out scale, out score);

            for (var i = 0; i < score.Length; i++)
            {
                result.Matches.Add(new ShapeMatchItem
                {
                    Center = new Point2D(Math.Round(column[i].D, 3), Math.Round(row[i].D, 3)),
                    AngleDeg = Math.Round(angle[i].D * 180.0 / Math.PI, 3),
                    Scale = Math.Round(scale[i].D, 3),
                    Score = Math.Round(score[i].D, 3),
                });
            }

            return result;
        }
        finally
        {
            row?.Dispose();
            column?.Dispose();
            angle?.Dispose();
            scale?.Dispose();
            score?.Dispose();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeManaged。
    /// 正文中的关键协作调用包括 DisposeManaged；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void DisposeManaged()
    {
        _disposed = true;
        _modelId?.Dispose();
        _modelId = null;
        base.DisposeManaged();
    }
}
