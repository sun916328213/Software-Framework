using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Models;

/// <summary>
/// 表示一次不可变的质量化值更新，使值、质量、时间与错误始终作为同一个快照被观察。
/// </summary>
/// <typeparam name="T">值的类型。</typeparam>
public sealed record ValueUpdate<T>
{
    /// <summary>创建一次质量化值更新。</summary>
    public ValueUpdate(
        T? value,
        DataQuality quality,
        DateTimeOffset timestamp,
        DateTimeOffset? sourceTimestamp = null,
        Exception? error = null)
    {
        Value = value;
        Quality = quality;
        Timestamp = timestamp;
        SourceTimestamp = sourceTimestamp;
        Error = error;
    }

    /// <summary>本次更新携带的值。</summary>
    public T? Value { get; init; }

    /// <summary>本次更新的数据质量。</summary>
    public DataQuality Quality { get; init; }

    /// <summary>本地接收或确认本次更新的时间。</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>数据源提供的时间；协议未提供时为空。</summary>
    public DateTimeOffset? SourceTimestamp { get; init; }

    /// <summary>质量为 Bad 时的失败原因。</summary>
    public Exception? Error { get; init; }
}
