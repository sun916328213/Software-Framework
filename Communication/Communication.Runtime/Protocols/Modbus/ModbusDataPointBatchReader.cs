using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;

namespace CommunicationNew.Protocols.Modbus;

/// <summary>在 Modbus TCP 和 RTU adapter 之间共用批量计划与响应切片逻辑。</summary>
internal static class ModbusDataPointBatchReader
{
    /// <summary>执行批量计划，并将每个批次转换为逐点结果。</summary>
    public static async Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
        IReadOnlyList<IDataPoint> points,
        Func<string, int, CancellationToken, Task<byte[]>> readAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(readAsync);
        var results = new List<DataPointReadResult>(points.Count);
        foreach (var batch in ModbusReadBatchPlanner.Plan(points))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var address = new ModbusAddressValue(
                    batch.Region,
                    batch.StartAddress).ToString();
                var response = await readAsync(
                        address,
                        batch.Quantity,
                        cancellationToken)
                    .ConfigureAwait(false);
                results.AddRange(batch.ExtractResponse(response));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 一次请求失败只影响该批次，后续非相邻批次仍可继续采样。
                results.AddRange(batch.Slices.Select(slice =>
                    DataPointReadResult.Failure(slice.Point, ex)));
            }
        }

        return results;
    }
}
