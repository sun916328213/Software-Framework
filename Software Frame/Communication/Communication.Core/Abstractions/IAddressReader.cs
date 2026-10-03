// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IAddressReader。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// 按协议地址读取原始数据的最小能力接口。
/// 地址和长度的具体含义由协议 adapter 定义，调用方无需依赖设备生命周期、写入或原始收发能力。
/// </summary>
public interface IAddressReader
{
    /// <summary>读取指定协议地址对应的原始数据。</summary>
    /// <param name="address">协议定义的地址。</param>
    /// <param name="length">协议定义的读取长度。</param>
    /// <param name="cancellationToken">取消当前读取操作的令牌。</param>
    Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken cancellationToken = default);
}
