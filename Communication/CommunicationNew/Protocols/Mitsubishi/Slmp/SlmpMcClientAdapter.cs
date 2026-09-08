using CommunicationNew.Abstractions;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>
/// 通过 SLMP 3E/4E 二进制 TCP 报文执行 MC 请求的 adapter。
/// 此 adapter 拥有 transport、codec、请求队列和自动重连生命周期。
/// </summary>
internal sealed class SlmpMcClientAdapter : IMcClientAdapter
{
    private readonly ConnectionSession<McRequest, McResponse> _session;

    /// <summary>使用指定字节流 transport 创建 SLMP adapter，并接管其生命周期。</summary>
    public SlmpMcClientAdapter(McParam parameter, IByteStreamTransport transport)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(transport);
        _session = new ConnectionSession<McRequest, McResponse>(
            transport,
            new McBinaryCodec(CreateCodecOptions(parameter)),
            TimeSpan.FromMilliseconds(parameter.CommunicationTimeout),
            reconnectOptions: parameter.AutoReconnect
                ? new ConnectionReconnectOptions(
                    TimeSpan.FromMilliseconds(parameter.ReconnectIntervalMs))
                : null,
            maxQueuedRequests: parameter.RequestQueueCapacity);
    }

    /// <inheritdoc />
    public ConnectionState State => _session.State;

    /// <inheritdoc />
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
        _session.ConnectAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) =>
        _session.DisconnectAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<McResponse> ExecuteAsync(
        McRequest request,
        CancellationToken cancellationToken = default) =>
        _session.ExecuteAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static McCodecOptions CreateCodecOptions(McParam parameter)
    {
        var frameFormat = parameter.FrameType.Trim().ToUpperInvariant() switch
        {
            "3E" => McFrameFormat.ThreeE,
            "4E" => McFrameFormat.FourE,
            _ => throw new ArgumentException("MC 帧类型只支持 3E 或 4E。", nameof(parameter)),
        };
        var timer = Math.Clamp(
            (int)Math.Ceiling(parameter.CommunicationTimeout / 250d),
            1,
            ushort.MaxValue);
        return new McCodecOptions
        {
            FrameFormat = frameFormat,
            NetworkNumber = parameter.NetworkNo,
            StationNumber = parameter.StationNo,
            ModuleIoNumber = parameter.ModuleNo,
            ModuleStationNumber = 0,
            MonitoringTimer = (ushort)timer,
        };
    }
}
