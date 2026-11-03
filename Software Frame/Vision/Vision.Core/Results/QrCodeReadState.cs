#nullable enable

namespace Vision.Core.Results;

/// <summary>单目标QR文本读取状态；找到四角不等于成功解码，原生异常不是其中任一空读码状态。</summary>
public enum QrCodeReadState
{
    NotDetected = 0,
    DetectedUndecodable = 1,
    Decoded = 2
}
