namespace CommunicationNew.Protocols.Modbus;

/// <summary>经过数量和负载校验的 Modbus RTU 请求。</summary>
public sealed class ModbusRtuRequest
{
    private ModbusRtuRequest(
        byte unitId,
        byte functionCode,
        ModbusAddressValue address,
        ushort quantity,
        ReadOnlyMemory<byte> payload)
    {
        UnitId = unitId;
        FunctionCode = functionCode;
        Address = address;
        Quantity = quantity;
        Payload = payload;
    }

    /// <summary>目标从站地址。</summary>
    public byte UnitId { get; }

    /// <summary>Modbus 功能码。</summary>
    public byte FunctionCode { get; }

    /// <summary>包含区域和协议零基起始位置的强类型地址。</summary>
    public ModbusAddressValue Address { get; }

    /// <summary>协议中的零基起始地址，供线格式 codec 使用。</summary>
    public ushort StartAddress => Address.StartAddress;

    /// <summary>线圈或寄存器的元素数量。</summary>
    public ushort Quantity { get; }

    /// <summary>写请求的数据区；读请求为空。</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>创建功能码 01 至 04 的读请求。</summary>
    public static ModbusRtuRequest Read(
        byte unitId,
        ModbusAddressValue address,
        ushort quantity)
    {
        var region = address.Region;
        ValidateQuantity(region, quantity, isWrite: false);
        var functionCode = region switch
        {
            ModbusRegion.Coil => (byte)1,
            ModbusRegion.DiscreteInput => (byte)2,
            ModbusRegion.HoldingRegister => (byte)3,
            ModbusRegion.InputRegister => (byte)4,
            _ => throw new ArgumentOutOfRangeException(nameof(region)),
        };
        return new ModbusRtuRequest(
            unitId,
            functionCode,
            address,
            quantity,
            ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>使用拆分地址参数创建读请求；请优先使用强类型地址重载。</summary>
    [Obsolete("请使用包含区域和零基位置的 ModbusAddressValue 重载。")]
    public static ModbusRtuRequest Read(
        byte unitId,
        ModbusRegion region,
        ushort startAddress,
        ushort quantity) =>
        Read(unitId, new ModbusAddressValue(region, startAddress), quantity);

    /// <summary>创建功能码 15 或 16 的批量写请求。</summary>
    public static ModbusRtuRequest Write(
        byte unitId,
        ModbusAddressValue address,
        ushort quantity,
        ReadOnlyMemory<byte> payload)
    {
        var region = address.Region;
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

        return new ModbusRtuRequest(
            unitId,
            region == ModbusRegion.Coil ? (byte)15 : (byte)16,
            address,
            quantity,
            payload);
    }

    /// <summary>使用拆分地址参数创建写请求；请优先使用强类型地址重载。</summary>
    [Obsolete("请使用包含区域和零基位置的 ModbusAddressValue 重载。")]
    public static ModbusRtuRequest Write(
        byte unitId,
        ModbusRegion region,
        ushort startAddress,
        ushort quantity,
        ReadOnlyMemory<byte> payload) =>
        Write(
            unitId,
            new ModbusAddressValue(region, startAddress),
            quantity,
            payload);

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
