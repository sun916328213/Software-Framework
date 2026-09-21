using Device.Core.Devices;

namespace Device.Core.Rfid;

/// <summary>
/// 按固定大小页读写数据的 RFID 设备接口。
/// </summary>
public interface IRfidDevice : IDevice
{
    /// <summary>创建设备时使用的配置快照。</summary>
    RfidOptions Options { get; }

    /// <summary>从指定的一基页码开始连续读取数据。</summary>
    /// <param name="startPage">从 1 开始的页码。</param>
    /// <param name="pageCount">需要读取的页数。</param>
    /// <param name="cancellationToken">取消本次读取。</param>
    /// <returns>成功时返回去除末尾空字符后的 UTF-8 文本。</returns>
    ValueTask<DeviceResult<string>> ReadPagesAsync(
        int startPage,
        int pageCount,
        CancellationToken cancellationToken = default);

    /// <summary>从指定的一基页码开始依次写入各页文本。</summary>
    /// <param name="startPage">从 1 开始的页码。</param>
    /// <param name="pages">每个元素对应一页，短页由驱动补零。</param>
    /// <param name="cancellationToken">取消本次写入。</param>
    /// <returns>包含写入是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> WritePagesAsync(
        int startPage,
        IReadOnlyList<string> pages,
        CancellationToken cancellationToken = default);
}
