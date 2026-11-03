using Vision.Core.Imaging;

namespace Vision.Controls.Models;

/// <summary>包内原始像素的描述；保留格式、行填充、帧信息，不降位深或转灰度。</summary>
public sealed class VisionProjectImage
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Stride { get; set; }
    public PixelFormat Format { get; set; }
    public int ByteLength { get; set; }
    public long FrameId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}
