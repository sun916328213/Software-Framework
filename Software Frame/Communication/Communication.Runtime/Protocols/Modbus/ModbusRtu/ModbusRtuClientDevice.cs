// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusRtuClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Modbus.ModbusRtu、Communication.Runtime.Protocols.Modbus、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Protocols.Modbus;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Runtime;
using Communication.Runtime.Transports;

namespace Communication.Runtime.Protocols.Modbus.ModbusRtu;

/// <summary>
/// Modbus RTU 客户端兼容 adapter。
/// 旧地址读写接口保留，内部使用 Serial transport、RTU codec 和 ConnectionSession。
/// </summary>
public class ModbusRtuClientDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter, IDataPointBatchReader
{
    private new ModbusRtuParam Param => (ModbusRtuParam)base.Param;
    private readonly ConnectionSession<ModbusRtuRequest, ModbusRtuResponse> _session;

    /// <summary>使用 Modbus RTU 参数创建生产串口 transport。</summary>
    public ModbusRtuClientDevice(ModbusRtuParam param)
        : this(param, CreateTransport(param))
    {
    }

    /// <summary>
    /// 使用指定字节流 transport 创建客户端；session 负责 transport 的最终释放。
    /// </summary>
    public ModbusRtuClientDevice(ModbusRtuParam param, IByteStreamTransport transport)
        : base(param)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ProtocolType = global::Communication.Core.ProtocolType.ModbusRtu;
        _session = new ConnectionSession<ModbusRtuRequest, ModbusRtuResponse>(
            transport,
            new ModbusRtuCodec(),
            TimeSpan.FromMilliseconds(TimeoutMs),
            reconnectOptions: param.AutoReconnect
                ? new ConnectionReconnectOptions(TimeSpan.FromMilliseconds(param.ReconnectIntervalMs))
                : null,
            maxQueuedRequests: param.RequestQueueCapacity);
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _session.State;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync、OnConnectionStateChanged、OnErrorOccurred；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (_session.State == ConnectionState.Connected)
        {
            return true;
        }

        try
        {
            await _session.ConnectAsync(ct).ConfigureAwait(false);
            OnConnectionStateChanged(
                true,
                $"Modbus RTU客户端已连接: {Param.PortName} (SlaveId={Param.SlaveId})");
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
    /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync、OnConnectionStateChanged；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _session.DisconnectAsync(ct).ConfigureAwait(false);
        OnConnectionStateChanged(false, "Modbus RTU客户端已断开");
    }

    /// <inheritdoc />
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 ParseValue、Read、ExecuteWithRetryAsync、ConfigureAwait、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken ct = default)
    {
        var (parsedAddress, quantity) = ModbusAddress.ParseValue(address, length);
        var request = ModbusRtuRequest.Read(
            Param.SlaveId,
            parsedAddress,
            quantity);
        return ExecuteWithRetryAsync(async () =>
        {
            var response = await _session.ExecuteAsync(request, ct).ConfigureAwait(false);
            ValidateResponse(request, response);
            return response.Data.ToArray();
        }, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、ParseValue、CopyTo、Write、ConfigureAwait；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task<bool> WriteAsync(
        string address,
        byte[] data,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
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

        var request = ModbusRtuRequest.Write(
            Param.SlaveId,
            parsedAddress,
            quantity,
            payload);
        // 写操作默认不重试，避免响应丢失时重复执行非幂等写入。
        var response = await _session.ExecuteAsync(request, ct).ConfigureAwait(false);
        ValidateResponse(request, response);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// </remarks>
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus RTU 不支持原始字节发送，请使用 ReadAsync/WriteAsync。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// </remarks>
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus RTU 不支持原始收发，请使用 ReadAsync/WriteAsync。");

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("Modbus RTU 连接失败。");
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
    /// 处理当前类型提供的操作；对应入口为 ValidateResponse。
    /// </summary>
    private static void ValidateResponse(
        ModbusRtuRequest request,
        ModbusRtuResponse response)
    {
        if (response.UnitId != request.UnitId)
        {
            throw new ModbusProtocolException(
                $"响应从站地址 {response.UnitId} 与请求 {request.UnitId} 不一致。");
        }
        if (response.FunctionCode != request.FunctionCode)
        {
            throw new ModbusProtocolException(
                $"响应功能码 {response.FunctionCode} 与请求 {request.FunctionCode} 不一致。");
        }
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateTransport。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </summary>
    private static SerialByteStreamTransport CreateTransport(ModbusRtuParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        return new SerialByteStreamTransport(new SerialTransportOptions(param.PortName, param.BaudRate)
        {
            DataBits = param.DataBits,
            StopBits = param.StopBits,
            Parity = param.Parity,
            TimeoutMs = param.TimeoutMs,
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 GetResult、GetAwaiter、AsTask、DisposeAsync、Dispose；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
    }
}
