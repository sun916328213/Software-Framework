#nullable enable

namespace Vision.Core.Results;

/// <summary>有限测量标量、明确含义及单位；不携带图像/SDK句柄，不代表真实精度承诺。</summary>
public sealed class ScalarMeasurementOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public GeometryMeasurementKind Kind
    {
        get;
    }

    public double Value
    {
        get;
    }

    public GeometryMeasurementUnit Unit => Kind is GeometryMeasurementKind.PointDistance or GeometryMeasurementKind.PointLineDistance or GeometryMeasurementKind.CaliperPairMeanWidth or GeometryMeasurementKind.CaliperCircleDiameter
        ? GeometryMeasurementUnit.Pixels : GeometryMeasurementUnit.Degrees;

    public ScalarMeasurementOutput(Guid frameId, Guid coordinateSpaceId, GeometryMeasurementKind kind, double value)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || !Enum.IsDefined(kind) || !double.IsFinite(value)
            || kind is GeometryMeasurementKind.PointDistance or GeometryMeasurementKind.PointLineDistance or GeometryMeasurementKind.CaliperPairMeanWidth or GeometryMeasurementKind.CaliperCircleDiameter && value < 0
            || kind == GeometryMeasurementKind.UndirectedLineAngle && (value < 0 || value > 90)
            || kind == GeometryMeasurementKind.DirectedLineAngle && (value <= -180 || value > 180))
        {
            throw new ArgumentException("几何标量的来源、含义或有限数值范围无效。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Kind = kind;
        Value = value;
    }
}
