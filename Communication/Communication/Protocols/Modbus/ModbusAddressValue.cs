namespace CommunicationNew.Protocols.Modbus;

/// <summary>
/// 强类型 Modbus 地址。
/// <see cref="StartAddress"/> 使用协议零基地址，字符串输入使用常见的一基地址表示。
/// </summary>
public readonly record struct ModbusAddressValue(
    ModbusRegion Region,
    ushort StartAddress)
{
    /// <summary>解析 40001、HR1、4x001、DI1、C1 等常见地址格式。</summary>
    /// <exception cref="FormatException">地址为空、前缀未知或一基地址小于 1。</exception>
    public static ModbusAddressValue Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("Modbus 地址不能为空。");
        }

        var value = text.Trim().ToUpperInvariant();
        if (value.Length == 5 && value.All(char.IsDigit))
        {
            var region = value[0] switch
            {
                '0' => ModbusRegion.Coil,
                '1' => ModbusRegion.DiscreteInput,
                '3' => ModbusRegion.InputRegister,
                '4' => ModbusRegion.HoldingRegister,
                _ => throw new FormatException($"不支持 Modbus 区域前缀 '{value[0]}'。"),
            };
            return new ModbusAddressValue(region, ParseOneBased(value[1..], text));
        }

        var (prefix, numberText) = value.Length > 2 &&
            value[0] is '0' or '1' or '3' or '4' &&
            value[1] == 'X'
            ? (value[..2], value[2..])
            : SplitPrefix(value);
        var parsedRegion = prefix switch
        {
            "HR" or "4X" or "" => ModbusRegion.HoldingRegister,
            "IR" or "3X" => ModbusRegion.InputRegister,
            "DI" or "1X" => ModbusRegion.DiscreteInput,
            "C" or "0X" => ModbusRegion.Coil,
            _ => throw new FormatException($"不支持 Modbus 地址前缀 '{prefix}'。"),
        };

        return new ModbusAddressValue(parsedRegion, ParseOneBased(numberText, text));
    }

    /// <summary>返回带区域前缀的一基规范地址。</summary>
    public override string ToString()
    {
        var prefix = Region switch
        {
            ModbusRegion.Coil => "C",
            ModbusRegion.DiscreteInput => "DI",
            ModbusRegion.InputRegister => "IR",
            ModbusRegion.HoldingRegister => "HR",
            _ => throw new ArgumentOutOfRangeException(nameof(Region)),
        };
        return $"{prefix}{StartAddress + 1}";
    }

    private static (string Prefix, string NumberText) SplitPrefix(string value)
    {
        var digitIndex = 0;
        while (digitIndex < value.Length && !char.IsDigit(value[digitIndex]))
        {
            digitIndex++;
        }

        return (value[..digitIndex], value[digitIndex..]);
    }

    private static ushort ParseOneBased(string numberText, string originalText)
    {
        if (!ushort.TryParse(numberText, out var oneBased) || oneBased == 0)
        {
            throw new FormatException($"Modbus 地址 '{originalText}' 必须包含大于零的数字地址。");
        }

        return (ushort)(oneBased - 1);
    }
}
