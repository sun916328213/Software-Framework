using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Modbus.ModbusTcp;

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
        ProtocolType = Core.ProtocolType.ModbusTcp;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _session?.State ?? ConnectionState.Disconnected;

    /// <inheritdoc />
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
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus TCP 不支持原始字节发送，请使用 ReadAsync/WriteAsync。");

    /// <inheritdoc />
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("Modbus TCP 不支持原始收发，请使用 ReadAsync/WriteAsync。");

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("Modbus TCP 连接失败。");
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

    private ConnectionSession<ModbusTcpRequest, ModbusTcpResponse> GetConnectedSession()
    {
        return _session is { State: ConnectionState.Connected } session
            ? session
            : throw new InvalidOperationException("设备未连接。");
    }

    private ConnectionReconnectOptions? CreateReconnectOptions() =>
        Param.AutoReconnect
            ? new ConnectionReconnectOptions(TimeSpan.FromMilliseconds(Param.ReconnectIntervalMs))
            : null;

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
