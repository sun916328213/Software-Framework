namespace CommunicationNew.Abstractions;

/// <summary>
/// 按协议地址写入原始数据的最小能力接口。
/// 地址与数据的具体含义由协议 adapter 定义；调用方不能据此假定写入可安全重试。
/// </summary>
public interface IAddressWriter
{
    /// <summary>向指定协议地址写入原始数据。</summary>
    /// <param name="address">协议定义的地址。</param>
    /// <param name="data">待写入的原始数据。</param>
    /// <param name="cancellationToken">取消当前写入操作的令牌。</param>
    /// <returns>协议 adapter 确认写入请求成功时返回 <see langword="true"/>。</returns>
    Task<bool> WriteAsync(
        string address,
        byte[] data,
        CancellationToken cancellationToken = default);
}
