#nullable enable

namespace Vision.Core.Operations;

/// <summary>圆卡尺沿名义圆径向扫描，极性按该方向解释。</summary>
public enum CircleScanDirection
{
    /// <summary>从圆内侧向外侧扫描。</summary>
    Outward = 0,

    /// <summary>从圆外侧向内侧扫描。</summary>
    Inward = 1
}
