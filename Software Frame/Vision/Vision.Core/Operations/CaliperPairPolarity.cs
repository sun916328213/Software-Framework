#nullable enable

namespace Vision.Core.Operations;

/// <summary>同一扫描剖面内条带两侧边缘的顺序，按实际扫描方向解释。</summary>
public enum CaliperPairPolarity
{
    /// <summary>先暗到亮，再亮到暗。</summary>
    LightStripe = 0,

    /// <summary>先亮到暗，再暗到亮。</summary>
    DarkStripe = 1
}
