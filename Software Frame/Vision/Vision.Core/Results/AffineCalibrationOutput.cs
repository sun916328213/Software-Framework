#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Diagnostics;

namespace Vision.Core.Results;

/// <summary>本次仿射标定及诊断；失败为NoMatch并带拒绝原因，不发布不可用矩阵或复用旧标定。</summary>
public sealed class AffineCalibrationOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid PixelSpaceId
    {
        get;
    }

    public Guid WorldSpaceId
    {
        get;
    }

    public AffinePixelCalibration? Calibration
    {
        get;
    }

    public VisionIssue? Rejection
    {
        get;
    }

    public double? DesignConditionNumber
    {
        get;
    }

    public double? TransformConditionNumber
    {
        get;
    }

    public IReadOnlyList<double> ResidualsMillimeters
    {
        get;
    }

    public AffineCalibrationOutput(Guid frameId, Guid pixelSpaceId, Guid worldSpaceId, AffinePixelCalibration? calibration,
        VisionIssue? rejection, double? designConditionNumber, double? transformConditionNumber, IEnumerable<double> residualsMillimeters)
    {
        ArgumentNullException.ThrowIfNull(residualsMillimeters);
        var residuals = residualsMillimeters.Take(4097).ToArray();
        if (frameId == Guid.Empty || pixelSpaceId == Guid.Empty || worldSpaceId == Guid.Empty
            || (calibration != null) == (rejection != null)
            || designConditionNumber is { } design && (!double.IsFinite(design) || design < 1)
            || transformConditionNumber is { } transform && (!double.IsFinite(transform) || transform < 1)
            || residuals.Length > 4096 || residuals.Any(value => !double.IsFinite(value) || value < 0)
            || calibration != null && (calibration.ReferenceFrameId != frameId || calibration.PixelSpaceId != pixelSpaceId
                || calibration.WorldSpaceId != worldSpaceId))
        {
            throw new ArgumentException("仿射标定结果来源、诊断或状态不一致。");
        }
        FrameId = frameId;
        PixelSpaceId = pixelSpaceId;
        WorldSpaceId = worldSpaceId;
        Calibration = calibration;
        Rejection = rejection;
        DesignConditionNumber = designConditionNumber;
        TransformConditionNumber = transformConditionNumber;
        ResidualsMillimeters = Array.AsReadOnly(residuals);
    }
}
