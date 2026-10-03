// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：FluentModbusTcpServerAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus、System.Net、FluentModbus。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus;
using System.Net;
using FluentModbus;

namespace Communication.Runtime.Protocols.Modbus.ModbusTcp;

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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Start。
    /// 正文中的关键协作调用包括 AddUnit、Start、Dispose；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Stop。
    /// 正文中的关键协作调用包括 Stop、Dispose；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// 正文中的关键协作调用包括 Read、GetRunningServer；具体条件和先后次序见方法体。
    /// </remarks>
    public byte[] Read(string address, int length) =>
        FluentModbusServerDataAccessor.Read(
            GetRunningServer(), _param.SlaveId, address, length);

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 Write。
    /// 正文中的关键协作调用包括 Write、GetRunningServer；具体条件和先后次序见方法体。
    /// </remarks>
    public void Write(string address, byte[] data)
        => FluentModbusServerDataAccessor.Write(
            GetRunningServer(), _param.SlaveId, address, data);

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose() => Stop();

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetRunningServer。
    /// </summary>
    private ModbusTcpServer GetRunningServer() =>
        _server ?? throw new InvalidOperationException("Modbus TCP 从站尚未启动。");
}
