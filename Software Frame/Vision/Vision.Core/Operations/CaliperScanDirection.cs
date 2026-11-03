#nullable enable

namespace Vision.Core.Operations;

/// <summary>扫描轴为有向基准线的右法向(dy,-dx)，并非面积ROI或图像翻转。</summary>
public enum CaliperScanDirection
{
    /// <summary>沿右法向从负半长扫描到正半长。</summary>
    Forward = 0,

    /// <summary>反向扫描，同时反转灰度变化的极性解释。</summary>
    Reverse = 1
}
