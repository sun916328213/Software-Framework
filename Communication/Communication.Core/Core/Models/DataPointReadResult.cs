using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Models;

/// <summary>表示批量读取中一个数据点的原始结果。</summary>
public sealed class DataPointReadResult
{
    private DataPointReadResult(IDataPoint point, byte[]? rawValue, Exception? error)
    {
        Point = point;
        RawValue = rawValue;
        Error = error;
    }

    /// <summary>结果对应的数据点。</summary>
    public IDataPoint Point { get; }

    /// <summary>成功读取的原始字节；失败时为空。</summary>
    public byte[]? RawValue { get; }

    /// <summary>读取错误；成功时为空。</summary>
    public Exception? Error { get; }

    /// <summary>创建成功结果。</summary>
    public static DataPointReadResult Success(IDataPoint point, byte[] rawValue)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(rawValue);
        return new DataPointReadResult(point, rawValue.ToArray(), null);
    }

    /// <summary>创建失败结果。</summary>
    public static DataPointReadResult Failure(IDataPoint point, Exception error)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(error);
        return new DataPointReadResult(point, null, error);
    }
}
