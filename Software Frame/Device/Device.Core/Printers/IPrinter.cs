// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IPrinter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.Printers;

/// <summary>
/// 网络标签打印机的公共接口，不暴露厂商连接和状态类型。
/// </summary>
public interface IPrinter : IDevice
{
    /// <summary>创建打印机时使用的配置快照。</summary>
    PrinterOptions Options { get; }

    /// <summary>读取打印机当前状态。</summary>
    /// <param name="cancellationToken">取消状态查询。</param>
    /// <returns>成功时返回结构化状态快照。</returns>
    ValueTask<DeviceResult<PrinterStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default);

    /// <summary>向打印机发送已经生成的原始打印语言数据。</summary>
    /// <param name="data">例如 ZPL 或 SBPL 指令的完整字节。</param>
    /// <param name="cancellationToken">取消尚未开始的发送操作。</param>
    /// <returns>包含发送是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> PrintRawAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}
