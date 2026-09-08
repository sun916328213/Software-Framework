using System.Net;
using FluentModbus;

namespace CommunicationNew.Protocols.Modbus.ModbusTcp;

/// <summary>基于 FluentModbus 的生产 TCP 从站 adapter。</summary>
internal sealed class FluentModbusTcpServerAdapter : IModbusTcpServerAdapter
{
    private readonly ModbusTcpParam _param;
    private ModbusTcpServer? _server;

    /// <summary>使用不可变设备配置创建 adapter。</summary>
    public FluentModbusTcpServerAdapter(ModbusTcpParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        _param = param;
    }

    /// <inheritdoc />
    public bool IsRunning => _server is not null;

    /// <inheritdoc />
    public void Start()
    {
        if (_server is not null)
        {
            return;
        }

        var server = new ModbusTcpServer();
        try
        {
            server.Start(new IPEndPoint(IPAddress.Any, _param.Port));
            _server = server;
        }
        catch
        {
            server.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        var server = _server;
        if (server is null)
        {
            return;
        }

        _server = null;
        server.Stop();
        server.Dispose();
    }

    /// <inheritdoc />
    public byte[] Read(string address, int length)
    {
        var server = GetRunningServer();
        var (parsedAddress, quantity) = ModbusAddress.ParseValue(address, length);
        var (region, startAddress) = parsedAddress;
        return region switch
        {
            ModbusRegion.Coil => server.GetCoils(_param.SlaveId)
                .Slice(startAddress / 8, (quantity + 7) / 8).ToArray(),
            ModbusRegion.DiscreteInput => server.GetDiscreteInputs(_param.SlaveId)
                .Slice(startAddress / 8, (quantity + 7) / 8).ToArray(),
            ModbusRegion.InputRegister => server.GetInputRegisters(_param.SlaveId)
                .Slice(startAddress, quantity).ToArray().ToBytes(),
            _ => server.GetHoldingRegisters(_param.SlaveId)
                .Slice(startAddress, quantity).ToArray().ToBytes(),
        };
    }

    /// <inheritdoc />
    public void Write(string address, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var server = GetRunningServer();
        var (parsedAddress, _) = ModbusAddress.ParseValue(address, data.Length);
        var (region, startAddress) = parsedAddress;

        switch (region)
        {
            case ModbusRegion.Coil:
                var coils = server.GetCoils(_param.SlaveId);
                for (var index = 0; index < data.Length; index++)
                {
                    var byteIndex = (startAddress + index) / 8;
                    var bitIndex = (startAddress + index) % 8;
                    if (data[index] != 0)
                    {
                        coils[byteIndex] |= (byte)(1 << bitIndex);
                    }
                    else
                    {
                        coils[byteIndex] &= (byte)~(1 << bitIndex);
                    }
                }
                break;
            case ModbusRegion.HoldingRegister:
                var values = data.ToShortArray();
                var registers = server.GetHoldingRegisters(_param.SlaveId);
                for (var index = 0; index < values.Length; index++)
                {
                    registers[startAddress + index] = (short)values[index];
                }
                break;
            default:
                throw new NotSupportedException($"{region} 是只读数据区，不能通过从站接口写入。");
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();

    private ModbusTcpServer GetRunningServer() =>
        _server ?? throw new InvalidOperationException("Modbus TCP 从站尚未启动。");
}
