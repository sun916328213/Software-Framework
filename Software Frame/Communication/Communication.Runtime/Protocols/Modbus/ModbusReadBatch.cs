// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusReadSlice、ModbusReadBatch。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;

namespace Communication.Runtime.Protocols.Modbus;

/// <summary>表示批量响应中一个数据点的字节切片。</summary>
public sealed record ModbusReadSlice
{
    /// <summary>创建数据点切片。</summary>
    public ModbusReadSlice(IDataPoint point, int byteOffset, int byteLength)
        : this(point, byteOffset, byteLength, bitOffset: -1)
    {
    }

    /// <summary>
    /// 初始化 ModbusReadSlice 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    internal ModbusReadSlice(
        IDataPoint point,
        int byteOffset,
        int byteLength,
        int bitOffset)
    {
        Point = point;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        BitOffset = bitOffset;
    }

    /// <summary>要更新的数据点。</summary>
    public IDataPoint Point { get; }

    /// <summary>在批量响应中的字节偏移。</summary>
    public int ByteOffset { get; }

    /// <summary>数据点需要的字节数。</summary>
    public int ByteLength { get; }

    /// <summary>位区域中的相对位偏移；寄存器切片为 -1。</summary>
    public int BitOffset { get; }
}

/// <summary>表示一次 Modbus 连续元素读取及其数据点切片。</summary>
public sealed record ModbusReadBatch
{
    /// <summary>创建一次连续寄存器读取。</summary>
    public ModbusReadBatch(
        ModbusRegion region,
        ushort startAddress,
        ushort quantity,
        IReadOnlyList<ModbusReadSlice> slices)
    {
        Region = region;
        StartAddress = startAddress;
        Quantity = quantity;
        Slices = slices;
    }

    /// <summary>寄存器区域。</summary>
    public ModbusRegion Region { get; }

    /// <summary>零基起始地址。</summary>
    public ushort StartAddress { get; }

    /// <summary>本次请求读取的线圈、离散输入或寄存器数量。</summary>
    public ushort Quantity { get; }

    /// <summary>本次请求数量的兼容属性；新代码应使用 <see cref="Quantity"/>。</summary>
    public ushort RegisterCount => Quantity;

    /// <summary>批量响应中的数据点切片。</summary>
    public IReadOnlyList<ModbusReadSlice> Slices { get; }

    /// <summary>
    /// 校验批量响应长度，并将各切片以同一采样时间发布到数据点。
    /// </summary>
    public void ApplyResponse(
        byte[] response,
        DateTimeOffset timestamp,
        DateTimeOffset? sourceTimestamp = null)
    {
        foreach (var result in ExtractResponse(response))
        {
            result.Point.ApplyRead(
                result.RawValue!,
                timestamp,
                sourceTimestamp);
        }
    }

    /// <summary>
    /// 校验批量响应，并返回每个数据点的独立原始结果。
    /// </summary>
    public IReadOnlyList<DataPointReadResult> ExtractResponse(byte[] response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var isBitRegion = Region is ModbusRegion.Coil or ModbusRegion.DiscreteInput;
        var expectedLength = isBitRegion ? (Quantity + 7) / 8 : Quantity * 2;
        if (response.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"Modbus 批量响应长度为 {response.Length}，预期 {expectedLength} 字节。");
        }

        return Slices.Select(slice =>
        {
            if (slice.BitOffset >= 0)
            {
                var packedByte = response[slice.BitOffset / 8];
                var value = (byte)((packedByte >> (slice.BitOffset % 8)) & 1);
                return DataPointReadResult.Success(slice.Point, new[] { value });
            }

            return DataPointReadResult.Success(
                slice.Point,
                response.AsSpan(slice.ByteOffset, slice.ByteLength).ToArray());
        }).ToArray();
    }
}
