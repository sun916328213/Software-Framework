using System;
using System.Globalization;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>集中维护 MC/MX 设备编号的进制和字符串格式。</summary>
internal static class McDeviceAddressFormatter
{
    public static int GetRadix(string prefix) => prefix switch
    {
        "X" or "Y" or "B" or "W" or "SB" or "SW" => 16,
        _ => 10,
    };

    public static string Format(string prefix, int number)
    {
        if (number is < 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }
        return prefix + (GetRadix(prefix) == 16
            ? number.ToString("X", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture));
    }
}
