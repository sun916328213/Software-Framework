#nullable enable

namespace Vision.Core.Results;

/// <summary>本阶段仅提供像素长度和角度，不允许把像素值改标签当毫米标定。</summary>
public enum GeometryMeasurementUnit
{
    /// <summary>像素长度。</summary>
    Pixels = 0,

    /// <summary>角度。</summary>
    Degrees = 1
}
