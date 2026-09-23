using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Mitsubishi;

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
        ProtocolType = Core.ProtocolType.MitsubishiMc;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _adapter.State;

    /// <inheritdoc />
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
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("MC codec 模式不支持绕过协议的原始发送，请使用 ReadAsync/WriteAsync。");

    /// <inheritdoc />
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("MC codec 模式不支持原始收发，请使用 ReadAsync/WriteAsync。");

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("MC 客户端连接失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

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

    private string CreateConnectionDescription()
    {
        if (Param.Backend == McClientBackend.MxComponent)
        {
            var mode = string.IsNullOrWhiteSpace(Param.MxBridgeExecutablePath)
                ? "直接模式"
                : "桥接模式";
            return $"MC协议客户端已连接: MX Component 逻辑站 {Param.MxLogicalStationNumber} ({mode})";
        }

        return $"MC协议客户端已连接: {Param.ServerIP}:{Param.Port} (帧类型:{Param.FrameType})";
    }

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

        if (string.IsNullOrWhiteSpace(param.MxBridgeExecutablePath))
        {
            return new MxComponentMcClientAdapter(
                param.MxLogicalStationNumber,
                param.MxPassword);
        }

        if (param.MxBridgeStartupTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(param.MxBridgeStartupTimeoutMs),
                param.MxBridgeStartupTimeoutMs,
                "MX 桥接启动超时必须大于零。");
        }

        return new MxComponentBridgeClientAdapter(
            new MxComponentBridgeOptions
            {
                ExecutablePath = param.MxBridgeExecutablePath,
                LogicalStationNumber = param.MxLogicalStationNumber,
                Password = param.MxPassword,
                StartupTimeout = TimeSpan.FromMilliseconds(param.MxBridgeStartupTimeoutMs),
                OperationTimeout = TimeSpan.FromMilliseconds(param.CommunicationTimeout),
            });
    }

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
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
    }
}
