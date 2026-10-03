// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusDataPointBatchReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;

namespace Communication.Runtime.Protocols.Modbus;

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
