#nullable enable

namespace Vision.Core.Operations;

/// <summary>
/// 明确RGB通道的8位编码值闭区间，三个通道同时满足才命中；不是灰度、HSV/Lab或感知色差。
/// 不猜gamma、白平衡、ICC或照明，阈值适用性由调用方验证。
/// </summary>
public sealed class RgbColorRange
{
    public byte MinimumRed
    {
        get;
    }
    public byte MaximumRed
    {
        get;
    }
    public byte MinimumGreen
    {
        get;
    }
    public byte MaximumGreen
    {
        get;
    }
    public byte MinimumBlue
    {
        get;
    }
    public byte MaximumBlue
    {
        get;
    }

    public RgbColorRange(byte minimumRed, byte maximumRed, byte minimumGreen, byte maximumGreen, byte minimumBlue, byte maximumBlue)
    {
        if (minimumRed > maximumRed || minimumGreen > maximumGreen || minimumBlue > maximumBlue)
        {
            throw new ArgumentException("RGB每个通道下限都不能超过上限，单位为0..255编码值。");
        }
        MinimumRed = minimumRed;
        MaximumRed = maximumRed;
        MinimumGreen = minimumGreen;
        MaximumGreen = maximumGreen;
        MinimumBlue = minimumBlue;
        MaximumBlue = maximumBlue;
    }
}
