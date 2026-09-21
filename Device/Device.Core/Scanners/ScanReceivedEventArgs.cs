namespace Device.Core.Scanners;

/// <summary>
/// 一条已经完成分帧和解码的扫码数据。
/// </summary>
public sealed class ScanReceivedEventArgs : EventArgs
{
    /// <summary>创建扫码数据事件。</summary>
    /// <param name="value">使用 UTF-8 解码后的条码内容。</param>
    /// <param name="rawData">不包含结束符的原始字节。</param>
    /// <param name="receivedAt">接收到完整帧的时间。</param>
    public ScanReceivedEventArgs(string value, ReadOnlyMemory<byte> rawData, DateTimeOffset receivedAt)
    {
        Value = value;
        RawData = rawData;
        ReceivedAt = receivedAt;
    }

    /// <summary>使用 UTF-8 解码后的条码内容。</summary>
    public string Value { get; }

    /// <summary>不包含结束符的原始字节。</summary>
    public ReadOnlyMemory<byte> RawData { get; }

    /// <summary>接收到完整帧的时间。</summary>
    public DateTimeOffset ReceivedAt { get; }
}
