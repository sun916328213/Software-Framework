// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ValueUpdate。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Interfaces;

namespace Communication.Core.Models;

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
