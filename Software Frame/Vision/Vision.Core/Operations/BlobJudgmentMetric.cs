#nullable enable

namespace Vision.Core.Operations;

/// <summary>区域结果的无图数值判定指标。</summary>
public enum BlobJudgmentMetric
{
    /// <summary>筛选后区域数量。</summary>
    Count = 0,

    /// <summary>明确指定区域的像素面积。</summary>
    Area = 1
}
