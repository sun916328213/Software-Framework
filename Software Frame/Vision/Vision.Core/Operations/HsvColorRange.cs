#nullable enable

namespace Vision.Core.Operations;

/// <summary>
/// 8位HSV量化编码闭区间：H为0..179（约2度一档），S/V为0..255，不是连续角度或归一化0..1。
/// H下限>上限明确跨179→0，0..179表示全色相；S8=0没有有效色相，是否参与须显式决定。
/// </summary>
public sealed class HsvColorRange
{
    public byte MinimumHue
    {
        get;
    }

    public byte MaximumHue
    {
        get;
    }

    public byte MinimumSaturation
    {
        get;
    }

    public byte MaximumSaturation
    {
        get;
    }

    public byte MinimumValue
    {
        get;
    }

    public byte MaximumValue
    {
        get;
    }

    public bool IncludeAchromatic
    {
        get;
    }

    public bool HueWraps => MinimumHue > MaximumHue;

    public HsvColorRange(byte minimumHue, byte maximumHue, byte minimumSaturation, byte maximumSaturation,
        byte minimumValue, byte maximumValue, bool includeAchromatic = false)
    {
        if (minimumHue > 179)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHue), "8位H量化编码只能为0..179，不接受角度或FULL编码。");
        }
        if (maximumHue > 179)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHue), "8位H量化编码只能为0..179，不接受角度或FULL编码。");
        }
        if (minimumSaturation > maximumSaturation || minimumValue > maximumValue)
        {
            throw new ArgumentException("S/V编码值范围须有序；仅H允许明确跨零点。");
        }
        MinimumHue = minimumHue;
        MaximumHue = maximumHue;
        MinimumSaturation = minimumSaturation;
        MaximumSaturation = maximumSaturation;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
        IncludeAchromatic = includeAchromatic;
    }
}
