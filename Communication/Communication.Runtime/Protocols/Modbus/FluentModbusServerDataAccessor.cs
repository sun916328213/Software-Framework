using FluentModbus;

namespace CommunicationNew.Protocols.Modbus;

/// <summary>在 FluentModbus 的同步锁内访问从站数据区，并统一地址与数据转换。</summary>
internal static class FluentModbusServerDataAccessor
{
    /// <summary>读取线圈、离散输入或寄存器数据。</summary>
    public static byte[] Read(
        ModbusServer server,
        byte unitId,
        string address,
        int length)
    {
        ArgumentNullException.ThrowIfNull(server);
        var (parsedAddress, quantity) = ModbusAddress.ParseValue(address, length);
        var (region, startAddress) = parsedAddress;

        // FluentModbus 默认异步处理客户端请求，应用访问其共享数据区时必须持有此锁。
        lock (server.Lock)
        {
            return region switch
            {
                ModbusRegion.Coil => ModbusServerDataCodec.ReadBits(
                    server.GetCoils(unitId), startAddress, quantity),
                ModbusRegion.DiscreteInput => ModbusServerDataCodec.ReadBits(
                    server.GetDiscreteInputs(unitId), startAddress, quantity),
                ModbusRegion.InputRegister => ModbusServerDataCodec.ReadRegisters(
                    server.GetInputRegisterBuffer(unitId), startAddress, quantity),
                _ => ModbusServerDataCodec.ReadRegisters(
                    server.GetHoldingRegisterBuffer(unitId), startAddress, quantity),
            };
        }
    }

    /// <summary>写入压缩线圈数据或大端保持寄存器数据。</summary>
    public static void Write(
        ModbusServer server,
        byte unitId,
        string address,
        byte[] data)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(data);
        var (parsedAddress, _) = ModbusAddress.ParseValue(address, data.Length);
        var (region, startAddress) = parsedAddress;

        lock (server.Lock)
        {
            switch (region)
            {
                case ModbusRegion.Coil:
                    ModbusServerDataCodec.WriteBits(
                        server.GetCoils(unitId), startAddress, data);
                    break;
                case ModbusRegion.HoldingRegister:
                    WriteHoldingRegisters(server, unitId, startAddress, data);
                    break;
                default:
                    throw new NotSupportedException($"{region} 是只读数据区，不能通过从站接口写入。");
            }
        }
    }

    private static void WriteHoldingRegisters(
        ModbusServer server,
        byte unitId,
        ushort startAddress,
        ReadOnlySpan<byte> data)
    {
        // FluentModbus 的原始寄存器缓冲区就是网络报文使用的大端字节序。
        // 直接访问该缓冲区可避免经 short 视图时受主机小端字节序影响。
        ModbusServerDataCodec.WriteRegisters(
            server.GetHoldingRegisterBuffer(unitId), startAddress, data);
    }
}
