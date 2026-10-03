// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusAddressValue。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Protocols.Modbus;

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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SplitPrefix。
    /// 正文中的关键协作调用包括 IsDigit；具体条件和先后次序见方法体。
    /// </summary>
    private static (string Prefix, string NumberText) SplitPrefix(string value)
    {
        var digitIndex = 0;
        while (digitIndex < value.Length && !char.IsDigit(value[digitIndex]))
        {
            digitIndex++;
        }

        return (value[..digitIndex], value[digitIndex..]);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ParseOneBased。
    /// 正文中的关键协作调用包括 TryParse；具体条件和先后次序见方法体。
    /// </summary>
    private static ushort ParseOneBased(string numberText, string originalText)
    {
        if (!ushort.TryParse(numberText, out var oneBased) || oneBased == 0)
        {
            throw new FormatException($"Modbus 地址 '{originalText}' 必须包含大于零的数字地址。");
        }

        return (ushort)(oneBased - 1);
    }
}
