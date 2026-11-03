#nullable enable

namespace Vision.Core.Operations;

/// <summary>已对齐同位深灰度图的原编码值差异方向，不做配准、归一化或光照补偿。</summary>
public enum ImageDifferenceDirection
{
    /// <summary>绝对差 |当前-参考|，同时检出变亮与变暗。</summary>
    Absolute = 0,
    /// <summary>只检测当前比参考变亮，反向差为0。</summary>
    CurrentBrighter = 1,
    /// <summary>只检测当前比参考变暗，反向差为0。</summary>
    CurrentDarker = 2
}
