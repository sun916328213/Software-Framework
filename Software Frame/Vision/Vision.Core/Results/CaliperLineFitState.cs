#nullable enable

namespace Vision.Core.Results;

/// <summary>未拟合是本次NoMatch，不借用上次直线，也不冒充SDK异常。</summary>
public enum CaliperLineFitState
{
    /// <summary>有效拟合。</summary>
    Fitted = 0,

    /// <summary>选中边缘点数量不足。</summary>
    InsufficientEdges = 1,

    /// <summary>残差剔除后的有效点数量不足。</summary>
    InsufficientInliers = 2,

    /// <summary>点集或投影跨度无法定义直线。</summary>
    Degenerate = 3
}
