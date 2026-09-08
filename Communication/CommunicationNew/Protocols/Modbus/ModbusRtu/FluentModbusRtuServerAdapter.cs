using FluentModbus;

namespace CommunicationNew.Protocols.Modbus.ModbusRtu;

/// <summary>基于 FluentModbus 的生产 RTU 从站 adapter。</summary>
internal sealed class FluentModbusRtuServerAdapter : IModbusRtuServerAdapter
{
    private readonly ModbusRtuParam _param;
    private ModbusRtuServer? _server;

    /// <summary>使用不可变设备配置创建 adapter。</summary>
    public FluentModbusRtuServerAdapter(ModbusRtuParam param)
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

        var server = new ModbusRtuServer(_param.SlaveId)
        {
            BaudRate = _param.BaudRate,
            Parity = _param.Parity,
            StopBits = _param.StopBits,
            ReadTimeout = _param.TimeoutMs,
            WriteTimeout = _param.TimeoutMs,
        };

        try
        {
            server.Start(_param.PortName);
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

    private ModbusRtuServer GetRunningServer() =>
        _server ?? throw new InvalidOperationException("Modbus RTU 从站尚未启动。");
}
