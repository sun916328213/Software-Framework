#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 整数灰度总体统计，无模型、无隐式位深/颜色转换；只统计处理域内的有效像素。
/// </summary>
public sealed class GrayStatisticsOperation : VisionImageOperation<GrayStatisticsOutput>
{
    public const string Id = "vision.measurement.gray-statistics";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;

    /// <summary>冻结的处理参与域，坐标属于本次输入图像；null 表示全图。</summary>
    public SearchRegion? Region
    {
        get;
    }

    public GrayStatisticsOperation(VisionFrame image, SearchRegion? region = null) : base(image)
    {
        if (region != null && !region.Runs(image.Width, image.Height).Any())
        {
            throw new ArgumentException("统计域与图像没有有效参与像素。", nameof(region));
        }

        Region = region;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
