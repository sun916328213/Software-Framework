// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：McRequest。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>经过地址、数量和负载校验的 MC 批量读写请求。</summary>
public sealed class McRequest
{
    /// <summary>
    /// 初始化 McRequest 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private McRequest(
        McAddressValue address,
        ushort quantity,
        bool isWrite,
        ReadOnlyMemory<byte> payload)
    {
        Address = address;
        Quantity = quantity;
        IsWrite = isWrite;
        Payload = payload;
    }

    /// <summary>访问起始地址。</summary>
    public McAddressValue Address { get; }

    /// <summary>访问点数。</summary>
    public ushort Quantity { get; }

    /// <summary>是否为批量写请求。</summary>
    public bool IsWrite { get; }

    /// <summary>已按协议格式编码的写数据。</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>创建批量读请求。</summary>
    public static McRequest Read(McAddressValue address, ushort quantity)
    {
        ValidateQuantity(address, quantity);
        return new McRequest(address, quantity, false, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>创建批量写请求，payload 必须已经使用 MC 二进制线格式。</summary>
    public static McRequest Write(
        McAddressValue address,
        ushort quantity,
        ReadOnlyMemory<byte> payload)
    {
        ValidateQuantity(address, quantity);
        var expectedLength = address.IsBit ? (quantity + 1) / 2 : quantity * 2;
        if (payload.Length != expectedLength)
        {
            throw new ArgumentException(
                $"地址 {address.Prefix} 写入 {quantity} 点需要 {expectedLength} 字节，实际为 {payload.Length}。",
                nameof(payload));
        }
        return new McRequest(address, quantity, true, payload);
    }

    /// <summary>在构造协议负载前校验点数，供同程序集中的兼容 adapter 避免无效分配。</summary>
    internal static void ValidateQuantity(McAddressValue address, ushort quantity)
    {
        if (string.IsNullOrWhiteSpace(address.Prefix))
        {
            throw new ArgumentException(
                "MC 请求必须使用经过解析的有效设备地址。",
                nameof(address));
        }
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "访问点数必须大于零。");
        }
        var max = address.IsBit ? 7168 : 960;
        if (quantity > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"{address.Prefix} 批量访问最多允许 {max} 点。");
        }
        if ((long)address.Number + quantity - 1 > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"{address} 起始的 {quantity} 点访问超出 24 位设备编号范围。");
        }
    }
}
