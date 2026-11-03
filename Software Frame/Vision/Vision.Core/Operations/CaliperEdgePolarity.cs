#nullable enable

namespace Vision.Core.Operations;

/// <summary>边缘极性按本次扫描方向解释，阈值单位为原位深灰度相邻采样差。</summary>
public enum CaliperEdgePolarity
{
    /// <summary>接受任一方向灰度变化。</summary>
    Any = 0,

    /// <summary>只接受灰度增加。</summary>
    DarkToLight = 1,

    /// <summary>只接受灰度减小。</summary>
    LightToDark = 2
}
