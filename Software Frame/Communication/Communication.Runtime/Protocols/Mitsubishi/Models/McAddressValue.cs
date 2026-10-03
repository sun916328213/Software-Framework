// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：McAddressValue、Descriptor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、System.Globalization。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using System.Globalization;

namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>包含设备代码、编号进制和位/字语义的 MC 地址值对象。</summary>
public readonly record struct McAddressValue
{
    private static readonly Descriptor[] Descriptors =
    [
        new("SM", 0x91, true),
        new("SD", 0xA9, false),
        new("SB", 0xA1, true),
        new("SW", 0xB5, false),
        new("ZR", 0xB0, false),
        new("TS", 0xC1, true),
        new("TC", 0xC0, true),
        new("TN", 0xC2, false),
        new("CS", 0xC4, true),
        new("CC", 0xC3, true),
        new("CN", 0xC5, false),
        new("M", 0x90, true),
        new("L", 0x92, true),
        new("F", 0x93, true),
        new("V", 0x94, true),
        new("B", 0xA0, true),
        new("S", 0x98, true),
        new("X", 0x9C, true),
        new("Y", 0x9D, true),
        // 旧 T/C 地址继续解释为当前值；新代码应使用明确的 TN/CN。
        new("T", 0xC2, false),
        new("C", 0xC5, false),
        new("D", 0xA8, false),
        new("W", 0xB4, false),
        new("R", 0xAF, false),
    ];

    /// <summary>
    /// 初始化 McAddressValue 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private McAddressValue(
        string prefix,
        byte deviceCode,
        int number,
        bool isBit)
    {
        Prefix = prefix;
        DeviceCode = deviceCode;
        Number = number;
        IsBit = isBit;
    }

    /// <summary>规范化设备前缀。</summary>
    public string Prefix { get; }

    /// <summary>二进制帧中的设备代码。</summary>
    public byte DeviceCode { get; }

    /// <summary>0 到 0xFFFFFF 的设备编号。</summary>
    public int Number { get; }

    /// <summary>该设备是否按位访问。</summary>
    public bool IsBit { get; }

    /// <summary>解析例如 D100、M10、B10 或 SM100 的 MC 地址。</summary>
    public static McAddressValue Parse(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var normalized = address.Trim().ToUpperInvariant();
        foreach (var descriptor in Descriptors)
        {
            if (!normalized.StartsWith(descriptor.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var numberText = normalized[descriptor.Prefix.Length..];
            if (numberText.Length == 0)
            {
                throw new FormatException($"MC 地址 {address} 缺少设备编号。");
            }
            var style = descriptor.Radix == 16
                ? NumberStyles.AllowHexSpecifier
                : NumberStyles.None;
            if (!int.TryParse(numberText, style, CultureInfo.InvariantCulture, out var number) ||
                number is < 0 or > 0xFFFFFF)
            {
                throw new FormatException($"MC 地址 {address} 的设备编号无效。");
            }
            return new McAddressValue(
                descriptor.Prefix,
                descriptor.DeviceCode,
                number,
                descriptor.IsBit);
        }
        throw new NotSupportedException($"不支持的 MC 设备地址: {address}。");
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ToString。
    /// 正文中的关键协作调用包括 FormatDevice；具体条件和先后次序见方法体。
    /// </remarks>
    public override string ToString() => FormatDevice(Number);

    /// <summary>返回保持设备类型不变、编号移动指定点数的新地址。</summary>
    public McAddressValue Offset(int offset)
    {
        var number = (long)Number + offset;
        if (number is < 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"地址 {this} 偏移 {offset} 后超出 24 位设备编号范围。");
        }
        return new McAddressValue(Prefix, DeviceCode, (int)number, IsBit);
    }

    /// <summary>使用当前设备类型规定的进制格式化指定编号。</summary>
    internal string FormatDevice(int number)
    {
        if (number is < 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }
        return McDeviceAddressFormatter.Format(Prefix, number);
    }

    /// <summary>
    /// Descriptor 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// </summary>
    private sealed record Descriptor(string Prefix, byte DeviceCode, bool IsBit)
    {
        public int Radix => McDeviceAddressFormatter.GetRadix(Prefix);
    }
}
