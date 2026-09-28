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
    public byte[] Read(string address, int length) =>
        FluentModbusServerDataAccessor.Read(
            GetRunningServer(), _param.SlaveId, address, length);

    /// <inheritdoc />
    public void Write(string address, byte[] data) =>
        FluentModbusServerDataAccessor.Write(
            GetRunningServer(), _param.SlaveId, address, data);

    /// <inheritdoc />
    public void Dispose() => Stop();

    private ModbusRtuServer GetRunningServer() =>
        _server ?? throw new InvalidOperationException("Modbus RTU 从站尚未启动。");
}
