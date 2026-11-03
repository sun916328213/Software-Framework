#nullable enable

namespace Vision.Core.Results;

/// <summary>数值的明确几何含义，不把无限直线距离称为边缘对宽度。</summary>
public enum GeometryMeasurementKind
{
    /// <summary>两点欧氏距离，像素。</summary>
    PointDistance = 0,

    /// <summary>点到无限直线的无符号正交距离，像素。</summary>
    PointLineDistance = 1,

    /// <summary>无向直线的锐夹角，0..90度。</summary>
    UndirectedLineAngle = 2,

    /// <summary>第一有向直线到第二条的屏幕顺时针有符号角，(-180,180]度。</summary>
    DirectedLineAngle = 3,

    /// <summary>同一剖面完整边缘对的扫描轴平均宽度，像素；不是两条拟合线距离。</summary>
    CaliperPairMeanWidth = 4,

    /// <summary>圆卡尺拟合直径，像素；未做毫米标定。</summary>
    CaliperCircleDiameter = 5
}
