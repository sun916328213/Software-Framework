// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：McClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Communication.Runtime.Transports。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Runtime.Transports;

namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>
/// 三菱 MC 客户端兼容 adapter。
/// 旧地址读写接口保留，后端可由 SLMP Socket 或其他 <see cref="IMcClientAdapter"/> 提供。
/// </summary>
public class McClientDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter
{
    private new McParam Param => (McParam)base.Param;
    private readonly IMcClientAdapter _adapter;

    /// <summary>使用 MC 参数创建生产 TCP transport。</summary>
    public McClientDevice(McParam param)
        : this(param, CreateAdapter(param))
    {
    }

    /// <summary>使用指定字节流 transport 创建 MC 客户端。</summary>
    public McClientDevice(McParam param, IByteStreamTransport transport)
        : this(param, new SlmpMcClientAdapter(param, transport))
    {
    }

    /// <summary>
    /// 使用指定 MC 后端 adapter 创建设备，并接管 adapter 生命周期。
    /// 此入口用于并存接入 SLMP、MX Component 桥接或其他受支持的三菱通信后端。
    /// </summary>
    public McClientDevice(McParam param, IMcClientAdapter adapter) : base(param)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        ProtocolType = global::Communication.Core.ProtocolType.MitsubishiMc;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _adapter.State;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync、OnConnectionStateChanged、CreateConnectionDescription、OnErrorOccurred；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (_adapter.State == ConnectionState.Connected)
        {
            return true;
        }
        try
        {
            await _adapter.ConnectAsync(ct).ConfigureAwait(false);
            OnConnectionStateChanged(
                true,
                CreateConnectionDescription());
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
        await _adapter.DisconnectAsync(ct).ConfigureAwait(false);
        OnConnectionStateChanged(false, "MC协议客户端已断开");
    }

    /// <summary>
    /// 按设备地址批量读取指定点数。
    /// 字设备与写入对称：adapter 返回 MC 小端线格式，此处换回大端字节供上层解码；
    /// 位设备保持打包原样，不做字节序交换。
    /// </summary>
    public override Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken ct = default)
    {
        if (length is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        var parsedAddress = McAddressValue.Parse(address);
        var request = McRequest.Read(parsedAddress, (ushort)length);
        return ExecuteWithRetryAsync(async () =>
        {
            var response = await _adapter.ExecuteAsync(request, ct).ConfigureAwait(false);
            var data = response.Data.ToArray();
            return parsedAddress.IsBit ? data : SwapWordByteOrder(data);
        }, ct);
    }

    /// <summary>
    /// 批量写入设备；字设备沿用旧接口的大端字节输入，adapter 转换为 MC 小端线格式，
    /// 与 <see cref="ReadAsync"/> 返回的大端字节对称。
    /// 位设备把每个输入字节解释为一个布尔点。
    /// </summary>
    public override async Task<bool> WriteAsync(
        string address,
        byte[] data,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var parsedAddress = McAddressValue.Parse(address);
        McRequest request;
        if (parsedAddress.IsBit)
        {
            var quantity = ValidateWriteQuantity(parsedAddress, data.Length);
            var packed = PackBits(data);
            request = McRequest.Write(
                parsedAddress,
                quantity,
                packed);
        }
        else
        {
            if (data.Length == 0 || (data.Length & 1) != 0)
            {
                throw new ArgumentException("MC 字设备写入数据必须是非空的偶数字节。", nameof(data));
            }
            var quantity = ValidateWriteQuantity(parsedAddress, data.Length / 2);
            request = McRequest.Write(
                parsedAddress,
                quantity,
                SwapWordByteOrder(data));
        }

        // 写操作默认不重试，避免响应丢失时重复执行非幂等 PLC 写入。
        await _adapter.ExecuteAsync(request, ct).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// </remarks>
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("MC codec 模式不支持绕过协议的原始发送，请使用 ReadAsync/WriteAsync。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// </remarks>
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("MC codec 模式不支持原始收发，请使用 ReadAsync/WriteAsync。");

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("MC 客户端连接失败。");
        }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateTransport。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private static IByteStreamTransport CreateTransport(McParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        if (!string.IsNullOrWhiteSpace(param.SerialPortName))
        {
            throw new NotSupportedException(
                "当前 MC adapter 仅实现官方 3E/4E TCP 二进制帧，不支持 MC Serial 模式。");
        }
        return new TcpByteStreamTransport(param.ServerIP, param.Port);
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateConnectionDescription。
    /// </summary>
    private string CreateConnectionDescription()
    {
        if (Param.Backend == McClientBackend.MxComponent)
        {
            return $"MC协议客户端已连接: MX Component 逻辑站 {Param.MxLogicalStationNumber} (直接模式)";
        }

        return $"MC协议客户端已连接: {Param.ServerIP}:{Param.Port} (帧类型:{Param.FrameType})";
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateAdapter。
    /// 正文中的关键协作调用包括 ThrowIfNull、CreateTransport、CreateMxComponentAdapter、nameof；具体条件和先后次序见方法体。
    /// </summary>
    private static IMcClientAdapter CreateAdapter(McParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        return param.Backend switch
        {
            McClientBackend.Slmp => new SlmpMcClientAdapter(param, CreateTransport(param)),
            McClientBackend.MxComponent => CreateMxComponentAdapter(param),
            _ => throw new ArgumentOutOfRangeException(
                nameof(param),
                param.Backend,
                "不支持的 MC 客户端后端。"),
        };
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateMxComponentAdapter。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    private static IMcClientAdapter CreateMxComponentAdapter(McParam param)
    {
        if (param.CommunicationTimeout <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(param.CommunicationTimeout),
                param.CommunicationTimeout,
                "MX Component 通信超时必须大于零。");
        }
        if (param.MxLogicalStationNumber is < 0 or > 1023)
        {
            throw new ArgumentOutOfRangeException(
                nameof(param.MxLogicalStationNumber),
                param.MxLogicalStationNumber,
                "MX Component 逻辑站号必须在 0 到 1023 之间。");
        }

        return new MxComponentMcClientAdapter(
            param.MxLogicalStationNumber,
            param.MxPassword);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateWriteQuantity。
    /// 正文中的关键协作调用包括 nameof、ValidateQuantity；具体条件和先后次序见方法体。
    /// </summary>
    private static ushort ValidateWriteQuantity(McAddressValue address, int quantity)
    {
        if (quantity is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "MC 写入点数必须在 1 到 65535 之间。");
        }

        var protocolQuantity = (ushort)quantity;
        // 先验证协议上限和访问末地址，再进行位打包或字节序复制。
        McRequest.ValidateQuantity(address, protocolQuantity);
        return protocolQuantity;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PackBits。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    private static byte[] PackBits(ReadOnlySpan<byte> values)
    {
        if (values.IsEmpty)
        {
            throw new ArgumentException("MC 位设备写入数据不能为空。", nameof(values));
        }
        var packed = new byte[(values.Length + 1) / 2];
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == 0)
            {
                continue;
            }
            packed[index / 2] |= index % 2 == 0 ? (byte)0x10 : (byte)0x01;
        }
        return packed;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SwapWordByteOrder。
    /// </summary>
    private static byte[] SwapWordByteOrder(ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length];
        for (var index = 0; index < data.Length; index += 2)
        {
            result[index] = data[index + 1];
            result[index + 1] = data[index];
        }
        return result;
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
            _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
    }
}
