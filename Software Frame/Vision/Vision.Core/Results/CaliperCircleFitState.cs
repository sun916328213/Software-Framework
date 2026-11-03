#nullable enable

namespace Vision.Core.Results;

/// <summary>本次圆拟合状态，未拟合时执行为NoMatch且不返回旧圆。</summary>
public enum CaliperCircleFitState
{
    /// <summary>有效拟合。</summary>
    Fitted = 0,

    /// <summary>选中边缘数量不足。</summary>
    InsufficientEdges = 1,

    /// <summary>残差筛选后内点不足。</summary>
    InsufficientInliers = 2,

    /// <summary>共线、病态或不可表示的点集。</summary>
    Degenerate = 3,

    /// <summary>内点围绕拟合圆心的覆盖角不足。</summary>
    InsufficientCoverage = 4
}
