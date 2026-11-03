using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Vision.Controls.Models;
using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>兼容JSON读取；新保存为单文件包，完整写入同目录临时文件后替换，不截断旧文件。</summary>
public sealed class VisionProjectFileStore : IVisionProjectFileStore
{
    private const string ManifestName = "project.json";
    private const string PixelsName = "images/input.bin";
    // 资源读取边界，防止压缩包声明尺寸触发无界分配；不是性能指标。
    public const int MaximumImageBytes = 512 * 1024 * 1024;
    private const int MaximumManifestBytes = 8 * 1024 * 1024;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public string Read(string path) => ReadProject(path).Json;
    public void Write(string path, string json) => Replace(path, temporary => File.WriteAllText(temporary, json, Utf8));

    public void WriteProject(string path, string json, VisionImage? image)
    {
        var project = VisionProjectCodec.Decode(json);
        byte[]? pixels = null;
        project.Version = 2;
        project.InputImage = null;
        if (image != null)
        {
            if (image.IsEmpty || image.Pixels.Length > MaximumImageBytes)
                throw new InvalidDataException("输入图像无效或超过工程包512MiB像素上限。");
            pixels = image.Pixels.ToArray();
            var valid = VisionImage.FromBuffer(image.Width, image.Height, image.Format, pixels, image.Stride, image.Source);
            project.InputImage = new()
            {
                Width = valid.Width, Height = valid.Height, Stride = valid.Stride, Format = valid.Format,
                ByteLength = pixels.Length, FrameId = image.FrameId, Timestamp = image.Timestamp,
                Source = image.Source, Sha256 = Convert.ToHexString(SHA256.HashData(pixels))
            };
        }
        var manifest = VisionProjectCodec.Encode(project);
        if (Utf8.GetByteCount(manifest) > MaximumManifestBytes) throw new InvalidDataException("工程清单超过8MiB上限。");
        Replace(path, temporary =>
        {
            using var archive = ZipFile.Open(temporary, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(archive.CreateEntry(ManifestName).Open(), Utf8))
                writer.Write(manifest);
            if (pixels != null)
            {
                using var stream = archive.CreateEntry(PixelsName, CompressionLevel.Optimal).Open();
                stream.Write(pixels);
            }
        });
    }

    public VisionProjectDocument ReadProject(string path)
    {
        using var file = File.OpenRead(path);
        var isPackage = file.ReadByte() == 'P' && file.ReadByte() == 'K';
        file.Position = 0;
        if (!isPackage)
        {
            if (file.Length > MaximumManifestBytes) throw new InvalidDataException("工程JSON超过8MiB上限。");
            using var reader = new StreamReader(file, Utf8, detectEncodingFromByteOrderMarks: true);
            return new(reader.ReadToEnd());
        }
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        if (archive.Entries.Count is < 1 or > 2 ||
            archive.Entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != archive.Entries.Count)
            throw new InvalidDataException("工程包条目重复或数量无效。");
        var manifestEntry = archive.GetEntry(ManifestName) ?? throw new InvalidDataException("工程包缺少清单。");
        if (manifestEntry.Length > MaximumManifestBytes) throw new InvalidDataException("工程清单过大。");
        string json;
        using (var reader = new StreamReader(manifestEntry.Open(), Utf8)) json = reader.ReadToEnd();
        var project = VisionProjectCodec.Decode(json);
        if (project.Version != 2) throw new InvalidDataException("工程包必须使用v2清单。");
        if (project.InputImage is not { } info)
        {
            if (archive.Entries.Count != 1) throw new InvalidDataException("工程包存在未声明的图像条目。");
            return new(json, IsPackage: true);
        }
        var entry = archive.GetEntry(PixelsName) ?? throw new InvalidDataException("工程包缺少输入像素。");
        if (info.ByteLength <= 0 || info.ByteLength > MaximumImageBytes || entry.Length != info.ByteLength)
            throw new InvalidDataException("工程图像长度无效或超过512MiB上限。");
        var bpp = info.Format switch
        {
            PixelFormat.Mono8 => 1, PixelFormat.Mono16 => 2,
            PixelFormat.Rgb24 or PixelFormat.Bgr24 => 3,
            PixelFormat.Bgra32 or PixelFormat.Float32 => 4, _ => 0
        };
        var row = (long)info.Width * bpp;
        var stride = info.Stride > 0 ? info.Stride : row;
        if (info.Width <= 0 || info.Height <= 0 || bpp == 0 || row > int.MaxValue ||
            stride < row || stride * info.Height > info.ByteLength)
            throw new InvalidDataException("工程图像尺寸/行字节数与像素长度不匹配。");
        var data = new byte[info.ByteLength];
        using (var stream = entry.Open()) stream.ReadExactly(data);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(data)), info.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("工程图像校验失败。");
        VisionImage image;
        try { image = VisionImage.FromBuffer(info.Width, info.Height, info.Format, data, info.Stride, info.Source); }
        catch (ArgumentException error) { throw new InvalidDataException("工程图像格式/尺寸无效。", error); }
        image.FrameId = info.FrameId; image.Timestamp = info.Timestamp;
        return new(json, image, true);
    }

    private static void Replace(string path, Action<string> write)
    {
        var fullPath = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            write(temporary);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
