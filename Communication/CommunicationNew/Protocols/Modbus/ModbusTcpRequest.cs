namespace CommunicationNew.Protocols.Modbus;

/// <summary>
/// 经过校验的 Modbus TCP 请求。
/// 地址和数量均使用协议中的零基地址与元素数量，不混用字节长度。
/// </summary>
public sealed class ModbusTcpRequest
{
    private ModbusTcpRequest(
        byte unitId,
        byte functionCode,
        ushort startAddress,
        ushort quantity,
        ReadOnlyMemory<byte> payload)
    {
        UnitId = unitId;
        FunctionCode = functionCode;
        StartAddress = startAddress;
        Quantity = quantity;
        Payload = payload;
    }

    /// <summary>从站单元标识。</summary>
    public byte UnitId { get; }

    /// <summary>Modbus 功能码。</summary>
    public byte FunctionCode { get; }

    /// <summary>协议中的零基起始地址。</summary>
    public ushort StartAddress { get; }

    /// <summary>线圈或寄存器的元素数量。</summary>
    public ushort Quantity { get; }

    /// <summary>写请求的数据区；读请求为空。</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>创建读线圈、离散输入、输入寄存器或保持寄存器请求。</summary>
    public static ModbusTcpRequest Read(
        byte unitId,
        ModbusRegion region,
        ushort startAddress,
        ushort quantity)
    {
        ValidateQuantity(region, quantity, isWrite: false);
        return new ModbusTcpRequest(
            unitId,
            GetReadFunctionCode(region),
            startAddress,
            quantity,
            ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>创建写多个线圈或写多个保持寄存器请求。</summary>
    public static ModbusTcpRequest Write(
        byte unitId,
        ModbusRegion region,
        ushort startAddress,
        ushort quantity,
        ReadOnlyMemory<byte> payload)
    {
        if (region is not (ModbusRegion.Coil or ModbusRegion.HoldingRegister))
        {
            throw new ArgumentException("只能写入线圈或保持寄存器。", nameof(region));
        }

        ValidateQuantity(region, quantity, isWrite: true);
        var expectedBytes = region == ModbusRegion.Coil
            ? (quantity + 7) / 8
            : quantity * 2;
        if (payload.Length != expectedBytes)
        {
            throw new ArgumentException(
                $"数量 {quantity} 需要 {expectedBytes} 字节，实际为 {payload.Length} 字节。",
                nameof(payload));
        }

        return new ModbusTcpRequest(
            unitId,
            region == ModbusRegion.Coil ? (byte)15 : (byte)16,
            startAddress,
            quantity,
            payload);
    }

    private static byte GetReadFunctionCode(ModbusRegion region) => region switch
    {
        ModbusRegion.Coil => 1,
        ModbusRegion.DiscreteInput => 2,
        ModbusRegion.HoldingRegister => 3,
        ModbusRegion.InputRegister => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(region)),
    };

    private static void ValidateQuantity(ModbusRegion region, ushort quantity, bool isWrite)
    {
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "数量必须大于零。");
        }

        var max = region is ModbusRegion.Coil or ModbusRegion.DiscreteInput
            ? (isWrite ? 1968 : 2000)
            : (isWrite ? 123 : 125);
        if (quantity > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"该操作最多允许 {max} 个元素。");
        }
    }
}
