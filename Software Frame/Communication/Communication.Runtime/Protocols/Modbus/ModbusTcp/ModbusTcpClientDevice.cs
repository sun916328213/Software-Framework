// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusTcpClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Runtime;
using Communication.Runtime.Transports;

namespace Communication.Runtime.Protocols.Modbus.ModbusTcp;

/// <summary>
/// Modbus TCP 客户端兼容适配器。
/// 对外保留旧 <see cref="ICommunicationDevice"/> 接口，内部将请求交给新的
/// <see cref="ConnectionSession{TRequest,TResponse}"/>。
/// </summary>
public class ModbusTcpClientDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter, IDataPointBatchReader
{
    private new ModbusTcpParam Param => (ModbusTcpParam)base.Param;
    private ConnectionSession<ModbusTcpRequest, ModbusTcpResponse>? _session;

    /// <summary>使用 Modbus TCP 参数创建设备。</summary>
    public ModbusTcpClientDevice(ModbusTcpParam param)
        : base(param)
    {
        ProtocolType = global::Communication.Core.ProtocolType.ModbusTcp;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _session?.State ?? ConnectionState.Disconnected;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、DisposeAsync、FromMilliseconds、CreateReconnectOptions、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (_session?.State == ConnectionState.Connected)
        {
            return true;
        }

        try
        {
            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
            }

            var transport = new TcpByteStreamTransport(Param.ServerIP, Param.Port);
            var session = new ConnectionSession<ModbusTcpRequest, ModbusTcpResponse>(
                transport,
                new ModbusTcpCodec(),
                TimeSpan.FromMilliseconds(TimeoutMs),
                reconnectOptions: CreateReconnectOptions(),
                maxQueuedRequests: Param.RequestQueueCapacity);
            await session.ConnectAsync(ct).ConfigureAwait(false);
            _session = session;

            OnConnectionStateChanged(
                true,
                $"Modbus TCP客户端已连接: {Param.ServerIP}:{Param.Port}");
            return true;
        }
        catch (OperationCanceledException)
        {
            OnConnectionStateChanged(false, "连接已取消");
            throw;
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex);
            OnConnectionStateChanged(false, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync、DisposeAsync、OnConnectionStateChanged；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task DisconnectAsync(CancellationToken ct = default)
    {
        var session = _session;
        _session = null;
        if (session is not null)
        {
            await session.DisconnectAsync(ct).ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
        }

        OnConnectionStateChanged(false, "Modbus TCP客户端已断开");
    }

    /// <inheritdoc />
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 GetConnectedSession、ParseValue、Read、ConfigureAwait、ExecuteWithRetryAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken ct = default)
    {
        var session = GetConnectedSession();
        var (parsedAddress, quantity) = ModbusAddress.ParseValue(address, length);
        var request = ModbusTcpRequest.Read(
            Param.SlaveId,
            parsedAddress,
            quantity);

        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await session.ExecuteAsync(request, ct).ConfigureAwait(false);
            ValidateResponse(request, response);
            return response.Data.ToArray();
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、GetConnectedSession、ParseValue、CopyTo、Write；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task<bool> WriteAsync(
        string address,
        byte[] data,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var session = GetConnectedSession();
        var (parsedAddress, _) = ModbusAddress.ParseValue(address, data.Length);
        var region = parsedAddress.Region;

        ReadOnlyMemory<byte> payload = data;
        ushort quantity;
        if (region == ModbusRegion.Coil)
        {
            quantity = checked((ushort)(data.Length * 8));
        }
        else
        {
            quantity = checked((ushort)((data.Length + 1) / 2));
            if ((data.Length & 1) != 0)
            {
                var padded = new byte[data.Length + 1];
                data.CopyTo(padded, 0);
                payload = padded;
            }
        }

        var request = ModbusTcpRequest.Write(
            Param.SlaveId,
            parsedAddress,
            quantity,
            payload);

        // 写操作默认不重试，避免在响应丢失时重复执行非幂等写入。
        var response = await session.ExecuteAsync(request, ct).ConfigureAwait(false);
        ValidateResponse(request, response);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// </remarks>
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus TCP 不支持原始字节发送，请使用 ReadAsync/WriteAsync。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// </remarks>
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus TCP 不支持原始收发，请使用 ReadAsync/WriteAsync。");

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("Modbus TCP 连接失败。");
        }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <inheritdoc />
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadPointsAsync。
    /// 正文中的关键协作调用包括 ReadPointsAsync；具体条件和先后次序见方法体。
    /// </remarks>
    Task<IReadOnlyList<DataPointReadResult>> IDataPointBatchReader.ReadPointsAsync(
        IReadOnlyList<IDataPoint> points,
        CancellationToken cancellationToken) =>
        ModbusDataPointBatchReader.ReadPointsAsync(
            points,
            ReadAsync,
            cancellationToken);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetConnectedSession。
    /// </summary>
    private ConnectionSession<ModbusTcpRequest, ModbusTcpResponse> GetConnectedSession()
    {
        return _session is { State: ConnectionState.Connected } session
            ? session
            : throw new InvalidOperationException("设备未连接。");
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateReconnectOptions。
    /// 正文中的关键协作调用包括 FromMilliseconds；具体条件和先后次序见方法体。
    /// </summary>
    private ConnectionReconnectOptions? CreateReconnectOptions() =>
        Param.AutoReconnect
            ? new ConnectionReconnectOptions(TimeSpan.FromMilliseconds(Param.ReconnectIntervalMs))
            : null;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateResponse。
    /// </summary>
    private static void ValidateResponse(
        ModbusTcpRequest request,
        ModbusTcpResponse response)
    {
        if (response.UnitId != request.UnitId)
        {
            throw new ModbusProtocolException(
                $"响应单元标识 {response.UnitId} 与请求 {request.UnitId} 不一致。");
        }

        if (response.FunctionCode != request.FunctionCode)
        {
            throw new ModbusProtocolException(
                $"响应功能码 {response.FunctionCode} 与请求 {request.FunctionCode} 不一致。");
        }
    }
}
