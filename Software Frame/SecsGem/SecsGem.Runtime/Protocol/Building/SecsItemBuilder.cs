using Secs4Net;

namespace SecsGem.Runtime.Protocol.Building;

/// <summary>按指定格式组包，保留旧入口的空值、转换异常和不支持类型返回 null 的约定。</summary>
internal static class SecsItemBuilder
{
    internal static Item? Build(object? data, SecsFormat format) => format switch
    {
        SecsFormat.ASCII => Item.A(data?.ToString() ?? string.Empty),
        SecsFormat.Binary => data is null ? Item.B() : Item.B(Convert.ToByte(data)),
        SecsFormat.U1 => data is null ? Item.U1() : Item.U1(Convert.ToByte(data)),
        SecsFormat.U2 => data is null ? Item.U2() : Item.U2(Convert.ToUInt16(data)),
        SecsFormat.U4 => data is null ? Item.U4() : Item.U4(Convert.ToUInt32(data)),
        SecsFormat.U8 => data is null ? Item.U8() : Item.U8(Convert.ToUInt64(data)),
        SecsFormat.I1 => data is null ? Item.I1() : Item.I1(Convert.ToSByte(data)),
        SecsFormat.I2 => data is null ? Item.I2() : Item.I2(Convert.ToInt16(data)),
        SecsFormat.I4 => data is null ? Item.I4() : Item.I4(Convert.ToInt32(data)),
        SecsFormat.I8 => data is null ? Item.I8() : Item.I8(Convert.ToInt64(data)),
        SecsFormat.Boolean => data is null ? Item.Boolean() : Item.Boolean(Convert.ToBoolean(data)),
        SecsFormat.List => data switch
        {
            null => Item.L(),
            List<string> values => Item.L(values.Select(Item.A)),
            Dictionary<string, string> pairs => Item.L(pairs.Select(pair => Item.L(Item.A(pair.Key), Item.A(pair.Value)))),
            _ => null
        },
        _ => null
    };
}
