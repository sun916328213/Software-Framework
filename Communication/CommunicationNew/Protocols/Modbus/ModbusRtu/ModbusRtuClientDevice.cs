using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Modbus.ModbusRtu;

/// <summary>
/// Modbus RTU 客户端兼容 adapter。
/// 旧地址读写接口保留，内部使用 Serial transport、RTU codec 和 ConnectionSession。
/// </summary>
public class ModbusRtuClientDevice : CommunicationDeviceBase, IConnection, IDataPointBatchReader
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
        ProtocolType = Core.ProtocolType.ModbusRtu;
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
    public override async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _session.DisconnectAsync(ct).ConfigureAwait(false);
        OnConnectionStateChanged(false, "Modbus RTU客户端已断开");
    }

    /// <inheritdoc />
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
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus RTU 不支持原始字节发送，请使用 ReadAsync/WriteAsync。");

    /// <inheritdoc />
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus RTU 不支持原始收发，请使用 ReadAsync/WriteAsync。");

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("Modbus RTU 连接失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <inheritdoc />
    Task<IReadOnlyList<DataPointReadResult>> IDataPointBatchReader.ReadPointsAsync(
        IReadOnlyList<IDataPoint> points,
        CancellationToken cancellationToken) =>
        ModbusDataPointBatchReader.ReadPointsAsync(
            points,
            ReadAsync,
            cancellationToken);

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
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
    }
}
