namespace Device.Core.FrameGrabbers;

/// <summary>
/// 已经脱离厂商原生缓冲区生命周期的一帧采集卡图像。
/// </summary>
/// <param name="Width">图像宽度，单位为像素。</param>
/// <param name="Height">图像高度，单位为像素。</param>
/// <param name="Stride">相邻两行起始地址之间的字节数。</param>
/// <param name="PixelFormat">与厂商 SDK 类型解耦的像素格式名称。</param>
/// <param name="Data">包含行填充的托管图像字节。</param>
/// <param name="Sequence">当前连接期间单调递增的帧序号。</param>
/// <param name="CapturedAt">适配器完成原生缓冲复制的 UTC 时间。</param>
public sealed record FrameGrabberFrame(
    int Width,
    int Height,
    int Stride,
    string PixelFormat,
    ReadOnlyMemory<byte> Data,
    long Sequence,
    DateTimeOffset CapturedAt);
