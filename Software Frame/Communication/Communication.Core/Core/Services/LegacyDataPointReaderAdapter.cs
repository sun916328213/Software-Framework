// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：LegacyAddressReaderAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Abstractions;

namespace Communication.Core.Services;

/// <summary>
/// 将未实现新能力接口的旧设备包装为按地址读取器。
/// 仅兼容入口依赖冻结接口，当前轮询实现始终依赖 <see cref="IAddressReader"/>。
/// </summary>
public sealed class LegacyAddressReaderAdapter(ICommunicationDevice device) : IAddressReader
{
    private readonly ICommunicationDevice _device =
        device ?? throw new ArgumentNullException(nameof(device));

    /// <inheritdoc />
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 ReadAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken cancellationToken = default) =>
        _device.ReadAsync(address, length, cancellationToken);
}
