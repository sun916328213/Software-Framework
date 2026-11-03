using Vision.Core.Capabilities;
using Vision.Core.Engine;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Common;

/// <summary>真正无SDK的公共数值实现，不是备用厂商，也不公开第二个业务入口。</summary>
internal static partial class CommonVisionOperations
{
    internal static IReadOnlyList<VisionOperationCapability> Capabilities
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        new VisionOperationCapability(BlobJudgmentOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(PointDistanceOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(PointLineDistanceOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(LineAngleOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(LineIntersectionOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(MeasurementRangeOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(GrayStatisticsRangeOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(PixelScaleCalibrationOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(LengthToMillimetersOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(MillimeterRangeOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(AffineCalibrationOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(PixelToMillimeterPointOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(MillimeterPointToPixelOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(CalibratedPointDistanceOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(PixelLineToMillimetersOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(CalibratedPointLineDistanceOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(QrTextEqualsOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(QrDecodedCountOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(AiPredictionJudgmentOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)
    });

    internal static bool Handles(VisionOperation operation) => operation is BlobJudgmentOperation
        or PointDistanceOperation or PointLineDistanceOperation or LineAngleOperation
        or LineIntersectionOperation or MeasurementRangeOperation or GrayStatisticsRangeOperation or PixelScaleCalibrationOperation
        or LengthToMillimetersOperation or MillimeterRangeOperation or AffineCalibrationOperation
        or PixelToMillimeterPointOperation or MillimeterPointToPixelOperation or CalibratedPointDistanceOperation
        or PixelLineToMillimetersOperation or CalibratedPointLineDistanceOperation or QrTextEqualsOperation or QrDecodedCountOperation
        or AiPredictionJudgmentOperation;

    internal static BackendOutput Execute(VisionOperation operation)
    {
        return operation switch
        {
            GrayStatisticsRangeOperation gray => new(new GrayStatisticsRangeOutput(gray.Source, gray.Metric, gray.Minimum, gray.Maximum)),
            AiPredictionJudgmentOperation prediction => JudgeAiPrediction(prediction),
            QrDecodedCountOperation count => new(new QrCountJudgmentOutput(count.Source, count.Minimum, count.Maximum,
                count.Source.DecodedCount >= count.Minimum && count.Source.DecodedCount <= count.Maximum)),
            QrTextEqualsOperation judgment => new(new QrTextJudgmentOutput(judgment.Source, judgment.ExpectedText,
                judgment.Source.State == QrCodeReadState.Decoded && string.Equals(judgment.Source.Text, judgment.ExpectedText, StringComparison.Ordinal))),
            BlobJudgmentOperation judgment => ExecuteBlobJudgment(judgment),
            PointDistanceOperation distance => MeasurePointDistance(distance),
            PointLineDistanceOperation distance => MeasurePointLineDistance(distance),
            LineAngleOperation angle => MeasureLineAngle(angle),
            LineIntersectionOperation intersection => IntersectLines(intersection),
            AffineCalibrationOperation calibration => CalibrateAffine(calibration),
            PixelToMillimeterPointOperation mapping => MapPixelPoint(mapping),
            MillimeterPointToPixelOperation mapping => ReprojectWorldPoint(mapping),
            CalibratedPointDistanceOperation distance => MeasureCalibratedPointDistance(distance),
            PixelLineToMillimetersOperation mapping => MapPixelLine(mapping),
            CalibratedPointLineDistanceOperation distance => MeasureCalibratedPointLineDistance(distance),
            PixelScaleCalibrationOperation calibration => CalibrateUniformPixelScale(calibration),
            LengthToMillimetersOperation conversion => ConvertLengthToMillimeters(conversion),
            MillimeterRangeOperation range => new BackendOutput(new MillimeterRangeOutput(range.Source,
                range.MinimumMillimeters, range.MaximumMillimeters,
                range.Source.ValueMillimeters >= range.MinimumMillimeters && range.Source.ValueMillimeters <= range.MaximumMillimeters)),
            MeasurementRangeOperation range => new BackendOutput(new MeasurementRangeOutput(range.Source,
                range.Minimum, range.Maximum, range.Source.Value >= range.Minimum && range.Source.Value <= range.Maximum)),
            _ => throw new NotSupportedException("未接入的公共几何/判定操作。")
        };
    }

    private static BackendOutput ExecuteBlobJudgment(BlobJudgmentOperation operation)
    {
        var value = ReadValue(operation);
        var passed = value >= operation.Minimum && value <= operation.Maximum;
        return new BackendOutput(new BlobJudgmentOutput(operation.Source.FrameId, operation.Source.CoordinateSpaceId,
            operation.Metric, operation.ItemIndex, value, operation.Minimum, operation.Maximum, passed));
    }

    internal static double ReadValue(BlobJudgmentOperation operation)
    {
        return operation.Metric switch
        {
            BlobJudgmentMetric.Count => operation.Source.Items.Count,
            BlobJudgmentMetric.Area => operation.Source.Items[operation.ItemIndex!.Value - 1].Area,
            _ => throw new NotSupportedException("未接入的区域判定指标。")
        };
    }
}
