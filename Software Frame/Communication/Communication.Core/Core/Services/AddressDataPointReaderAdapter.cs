// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：AddressDataPointReaderAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Abstractions、Communication.Core.Interfaces、Communication.Core.Models。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Abstractions;
using Communication.Core.Interfaces;
using Communication.Core.Models;

namespace Communication.Core.Services;

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
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadPointsAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、ThrowIfCancellationRequested、ConfigureAwait、ReadAsync、Add；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
