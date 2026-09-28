using CommunicationNew.Abstractions;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;

namespace CommunicationNew.Core.Services;

/// <summary>
/// 把按地址读取能力适配为统一的数据点批量读取 seam。
/// 此 adapter 不拥有读取器生命周期；单点失败会转换为对应点位的失败结果。
/// </summary>
public sealed class AddressDataPointReaderAdapter(IAddressReader reader)
    : IDataPointBatchReader
{
    private readonly IAddressReader _reader =
        reader ?? throw new ArgumentNullException(nameof(reader));

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
        IReadOnlyList<IDataPoint> points,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);
        var results = new List<DataPointReadResult>(points.Count);
        foreach (var point in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bytes = await _reader
                    .ReadAsync(point.Address, point.Length, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(DataPointReadResult.Success(point, bytes));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 单点失败属于该点位的结果，不能阻止同轮后续点位继续采样。
                results.Add(DataPointReadResult.Failure(point, ex));
            }
        }

        return results;
    }
}
