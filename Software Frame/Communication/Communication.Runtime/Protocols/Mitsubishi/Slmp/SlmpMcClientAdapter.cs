// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SlmpMcClientAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、Communication.Core.Abstractions、Communication.Runtime、Communication.Runtime.Transports。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using Communication.Core.Abstractions;
using Communication.Runtime;
using Communication.Runtime.Transports;

namespace Communication.Runtime.Protocols.Mitsubishi;

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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConnectAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
        _session.ConnectAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) =>
        _session.DisconnectAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ExecuteAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask<McResponse> ExecuteAsync(
        McRequest request,
        CancellationToken cancellationToken = default) =>
        _session.ExecuteAsync(request, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 DisposeAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateCodecOptions。
    /// 正文中的关键协作调用包括 ToUpperInvariant、Trim、nameof、Clamp、Ceiling；具体条件和先后次序见方法体。
    /// </summary>
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
