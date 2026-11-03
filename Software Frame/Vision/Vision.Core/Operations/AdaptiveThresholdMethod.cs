#nullable enable

namespace Vision.Core.Operations;

/// <summary>局部阈值使用的邻域统计方式。</summary>
public enum AdaptiveThresholdMethod
{
    /// <summary>局部均值减去Offset。</summary>
    Mean = 0,

    /// <summary>局部高斯加权均值减去Offset。</summary>
    Gaussian = 1
}
