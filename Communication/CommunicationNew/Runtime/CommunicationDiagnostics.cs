using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CommunicationNew.Runtime;

/// <summary>
/// CommunicationNew 的标准诊断入口。
/// 调用方可按名称接入 OpenTelemetry，而核心不依赖具体日志或 APM 实现。
/// </summary>
public static class CommunicationDiagnostics
{
    /// <summary>用于订阅通信 trace 的 ActivitySource 名称。</summary>
    public const string ActivitySourceName = "CommunicationNew";

    /// <summary>用于订阅通信指标的 Meter 名称。</summary>
    public const string MeterName = "CommunicationNew";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OperationCounter =
        Meter.CreateCounter<long>("communication.operations", unit: "{operation}");
    private static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("communication.operation.duration", unit: "ms");
    private static readonly UpDownCounter<long> ActiveConnections =
        Meter.CreateUpDownCounter<long>("communication.connections.active", unit: "{connection}");
    private static readonly Counter<long> SentBytes =
        Meter.CreateCounter<long>("communication.bytes.sent", unit: "By");
    private static readonly Counter<long> ReceivedBytes =
        Meter.CreateCounter<long>("communication.bytes.received", unit: "By");
    private static readonly UpDownCounter<long> QueuedRequests =
        Meter.CreateUpDownCounter<long>("communication.requests.queued", unit: "{request}");
    private static readonly Counter<long> ConnectionAttempts =
        Meter.CreateCounter<long>("communication.connection.attempts", unit: "{attempt}");
    private static readonly Counter<long> ReconnectAttempts =
        Meter.CreateCounter<long>("communication.reconnect.attempts", unit: "{attempt}");
    private static readonly UpDownCounter<long> QueuedEvents =
        Meter.CreateUpDownCounter<long>("communication.events.queued", unit: "{event}");
    private static readonly Counter<long> DroppedEvents =
        Meter.CreateCounter<long>("communication.events.dropped", unit: "{event}");
    private static readonly Counter<long> CoalescedEvents =
        Meter.CreateCounter<long>("communication.events.coalesced", unit: "{event}");

    internal static Activity? StartRequestActivity(CommunicationDiagnosticContext context)
    {
        var activity = ActivitySource.StartActivity("communication.request", ActivityKind.Client);
        context.ApplyTo(activity);
        return activity;
    }

    /// <summary>启动请求流水线中的低开销内部阶段 span。</summary>
    internal static Activity? StartPhaseActivity(
        string phase,
        CommunicationDiagnosticContext context)
    {
        var activity = ActivitySource.StartActivity(
            $"communication.{phase}",
            ActivityKind.Internal);
        context.ApplyTo(activity);
        return activity;
    }

    internal static void RecordRequest(
        TimeSpan duration,
        string outcome,
        CommunicationDiagnosticContext context)
    {
        var tags = context.CreateTags();
        tags.Add("operation", "request");
        tags.Add("outcome", outcome);
        OperationCounter.Add(1, tags);
        OperationDuration.Record(duration.TotalMilliseconds, tags);
    }

    /// <summary>记录活动连接数量的增减；每个 session 只能成对调用。</summary>
    internal static void RecordActiveConnection(
        long delta,
        CommunicationDiagnosticContext context) =>
        ActiveConnections.Add(delta, context.CreateTags());

    /// <summary>记录成功交给 transport 的请求字节数。</summary>
    internal static void RecordSentBytes(
        long count,
        CommunicationDiagnosticContext context) =>
        SentBytes.Add(count, context.CreateTags());

    /// <summary>记录 codec 成功消费的响应字节数。</summary>
    internal static void RecordReceivedBytes(
        long count,
        CommunicationDiagnosticContext context) =>
        ReceivedBytes.Add(count, context.CreateTags());

    /// <summary>记录正在等待单请求执行权的请求数量增减。</summary>
    internal static void RecordQueuedRequest(
        long delta,
        CommunicationDiagnosticContext context) =>
        QueuedRequests.Add(delta, context.CreateTags());

    /// <summary>按低基数结果记录连接尝试。</summary>
    internal static void RecordConnectionAttempt(
        string outcome,
        CommunicationDiagnosticContext context)
    {
        var tags = context.CreateTags();
        tags.Add("outcome", outcome);
        ConnectionAttempts.Add(1, tags);
    }

    /// <summary>按低基数结果记录 supervisor 的重连尝试。</summary>
    internal static void RecordReconnectAttempt(
        string outcome,
        CommunicationDiagnosticContext context)
    {
        var tags = context.CreateTags();
        tags.Add("outcome", outcome);
        ReconnectAttempts.Add(1, tags);
    }

    /// <summary>记录等待用户处理器消费的事件通知数量增减。</summary>
    internal static void RecordQueuedEvent(
        long delta,
        CommunicationDiagnosticContext context) =>
        QueuedEvents.Add(delta, context.CreateTags());

    /// <summary>按低基数事件类型记录有界队列丢弃量。</summary>
    internal static void RecordDroppedEvent(
        string eventType,
        CommunicationDiagnosticContext context)
    {
        var tags = context.CreateTags();
        tags.Add("event.type", eventType);
        DroppedEvents.Add(1, tags);
    }

    /// <summary>按低基数事件类型记录被新值覆盖的通知数量。</summary>
    internal static void RecordCoalescedEvent(
        string eventType,
        CommunicationDiagnosticContext context)
    {
        var tags = context.CreateTags();
        tags.Add("event.type", eventType);
        CoalescedEvents.Add(1, tags);
    }
}

/// <summary>一次连接使用的低基数协议和传输标签。</summary>
internal readonly record struct CommunicationDiagnosticContext(
    string Protocol,
    string Transport)
{
    /// <summary>把连接标签附加到活动；无监听器时不执行任何工作。</summary>
    public void ApplyTo(Activity? activity)
    {
        activity?.SetTag("protocol", Protocol);
        activity?.SetTag("transport", Transport);
    }

    /// <summary>创建供 Meter 使用的连接标签集合。</summary>
    public TagList CreateTags() => new()
    {
        { "protocol", Protocol },
        { "transport", Transport },
    };
}
