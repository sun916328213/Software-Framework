using CommunicationNew.Abstractions;

namespace CommunicationNew.Core.Services;

/// <summary>
/// 将未实现新能力接口的旧设备包装为按地址读取器。
/// 仅兼容入口依赖冻结接口，当前轮询实现始终依赖 <see cref="IAddressReader"/>。
/// </summary>
internal sealed class LegacyAddressReaderAdapter(ICommunicationDevice device) : IAddressReader
{
    private readonly ICommunicationDevice _device =
        device ?? throw new ArgumentNullException(nameof(device));

    /// <inheritdoc />
    public Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken cancellationToken = default) =>
        _device.ReadAsync(address, length, cancellationToken);
}
