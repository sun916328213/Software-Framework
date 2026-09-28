using CommunicationNew.Core.Models;

namespace CommunicationNew.Core.Interfaces;

/// <summary>
/// 声明设备能将多个数据点合并为更少的底层读取操作。
/// </summary>
public interface IDataPointBatchReader
{
    /// <summary>
    /// 读取给定点位并为每个输入点位返回一个成功或失败结果。
    /// </summary>
    Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
        IReadOnlyList<IDataPoint> points,
        CancellationToken cancellationToken = default);
}
