#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>
/// 本次文件内容快照的解码结果。每次读取创建新帧/空间，路径或内容指纹相同不代表同一采集帧。
/// 指纹用于内容追溯，不是原生资源或跨处理分支等价的凭据。
/// </summary>
public sealed class ImageFileOutput
{
    /// <summary>新建帧和坐标空间的托管像素快照，不依赖文件句柄或解码器存活。</summary>
    public VisionFrame Image
    {
        get;
    }

    /// <summary>本次解码内容的绝对来源路径。</summary>
    public string FilePath
    {
        get;
    }

    /// <summary>本次实际送入解码器的编码字节SHA256，固定64位十六进制。</summary>
    public string ContentSha256
    {
        get;
    }

    public ImageFileOutput(VisionFrame image, string filePath, string contentSha256)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException("必须注明解码文件的绝对路径。", nameof(filePath));
        }

        if (contentSha256.Length != 64 || contentSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("必须注明编码内容的SHA256。", nameof(contentSha256));
        }

        Image = image;
        FilePath = Path.GetFullPath(filePath);
        ContentSha256 = contentSha256.ToLowerInvariant();
    }
}
