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
            // TCP 从站默认不创建任何 Unit；必须先注册配置的 SlaveId，
            // 否则本地数据区访问和收到的网络请求都会以 UnitIdentifierNotFound 失败。
            server.AddUnit(_param.SlaveId);
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
    public byte[] Read(string address, int length) =>
        FluentModbusServerDataAccessor.Read(
            GetRunningServer(), _param.SlaveId, address, length);

    /// <inheritdoc />
    public void Write(string address, byte[] data)
        => FluentModbusServerDataAccessor.Write(
            GetRunningServer(), _param.SlaveId, address, data);

    /// <inheritdoc />
    public void Dispose() => Stop();

    private ModbusTcpServer GetRunningServer() =>
        _server ?? throw new InvalidOperationException("Modbus TCP 从站尚未启动。");
}
