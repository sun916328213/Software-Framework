using Secs4Net;
using SecsGem.Runtime.Protocol.Building;

namespace SecsGem.Runtime.Order.S9;

/// <summary>
/// S9F1/3/5/7/9/11 的原始 B[10] MHEAD/SHEAD，不含四字节 HSMS 长度前缀。
/// 字节 0–1：原 Device ID / Session ID，大端。
/// 字节 2：原 W 位（最高位）与 Stream（低七位）；字节 3：原 Function。
/// 字节 4–5：原 PType / SType，HSMS data message 均为 0。
/// 字节 6–9：原 System Bytes，大端，保留全部 32 位。
/// MHEAD 取自出错的接收消息；SHEAD 取自等待应答超时的发送消息，均不是 S9 自身的报头。
/// </summary>
public readonly record struct S9HeaderData(byte[] Header);

internal static class S9ProtocolCodec
{
    internal static Item Header(params object[] data)
    {
        if (data.Length != 1) throw new ArgumentException("S9 报头通知需要一个参数。");
        var bytes = data[0] switch
        {
            S9HeaderData value => value.Header,
            byte[] value => value,
            Item { Format: SecsFormat.Binary, Count: 10 } value => value.GetMemory<byte>().ToArray(),
            SS9F9 value when value.SecsMessage != null => HsmsMessageHeaderBuilder.GetHeaderInfo(value.SecsMessage, value.DeviceID, value.ID),
            _ => throw new ArgumentException("需要原始 B[10] 报头或 S9HeaderData。")
        };
        if (bytes is not { Length: 10 }) throw new ArgumentException("MHEAD/SHEAD 必须为十字节。");
        return Item.B(bytes.ToArray());
    }

    internal static string Description(byte function) => function switch
    {
        1 => "设备 ID 不识别", 3 => "Stream 不识别", 5 => "Function 不识别",
        7 => "数据非法", 9 => "事务应答超时 T3", 11 => "数据过长", 13 => "会话超时",
        _ => "未支持的错误通知"
    };

    internal static void Ascii(Item? item, string name, bool required = false)
    {
        if (item?.Format != SecsFormat.ASCII || item.GetString().Any(c => c > 127)
            || (required && item.Count == 0)) throw new ArgumentException($"{name} 要求{(required ? "非空 " : "")}ASCII。");
    }

    internal static string Describe(SecsMessage message)
    {
        if (message.F == 13)
        {
            if (message.SecsItem is not { Format: SecsFormat.List, Count: 2 } fields)
                throw new ArgumentException("S9F13 要求 L[2] MEXP, EDID。");
            Ascii(fields.Items[0], "MEXP", true);
            Ascii(fields.Items[1], "EDID");
            return $"会话超时，MEXP={fields.Items[0].GetString()}，EDID={fields.Items[1].GetString()}";
        }
        if (message.F is not (1 or 3 or 5 or 7 or 9 or 11)) throw new ArgumentException("未支持的 S9 通知。");
        if (message.SecsItem is not { Format: SecsFormat.Binary, Count: 10 } body)
            throw new ArgumentException("MHEAD/SHEAD 要求 B[10]。");
        return $"{Description(message.F)}，MHEAD/SHEAD={Convert.ToHexString(body.GetMemory<byte>().Span)}";
    }
}

internal abstract class S9HeaderError : SecsOrderBase
{
    public override Item GetSendItem(params object[] data)
    {
        try { return S9ProtocolCodec.Header(data); }
        catch (ArgumentException) { return null!; }
    }

    // 只校验通知，不生成 S9F2/F4 等虚假应答；Runtime 负责记录诊断。
    public override Item GetReplyItem(SecsMessage message)
    {
        S9ProtocolCodec.Describe(message);
        return null!;
    }
}
