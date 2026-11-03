using System.Text.Json;
using Xunit;
using Vision.Core;
using Vision.Tools.Halcon.Acquisition;

namespace Vision.Tests.Tools.Halcon;

[Trait("Category", "Contract")]
public sealed class ImageFilePathContractTests
{
    [Fact]
    public void PathMetadataAndNotifications_PreserveOriginalSerializedParameter()
    {
        var p = new ImageFileParameters(); var names = new List<string?>();
        p.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        p.FilePath = @"C:\图像\sample.tiff"; p.FilePath = @"C:\图像\sample.tiff";
        Assert.Equal(new[] { nameof(ImageFileParameters.FilePath) }, names);
        var json = JsonSerializer.Serialize(p);
        using var doc = JsonDocument.Parse(json);
        Assert.Single(doc.RootElement.EnumerateObject());
        Assert.Equal(p.FilePath, JsonSerializer.Deserialize<ImageFileParameters>(json)!.FilePath);
        var metadata = (ToolPropertyAttribute)Attribute.GetCustomAttribute(typeof(ImageFileParameters).GetProperty(nameof(p.FilePath))!, typeof(ToolPropertyAttribute))!;
        Assert.Contains("*.png", metadata.FileDialogFilter); Assert.Contains("*.tiff", metadata.FileDialogFilter);
    }

    [Fact]
    public void FileSource_DoesNotConvertUnrelatedTaskImage_BeforeCheckingItsOwnPath()
    {
        using var tool = new ImageFileTool();
        tool.Parameters.FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        using var result = tool.RunImage(Vision.Core.Imaging.VisionImage.FromBuffer(1, 1, Vision.Core.Imaging.PixelFormat.Bgr24, [1, 2, 3]));
        Assert.False(result.Success); Assert.Contains("图像文件不存在", result.Message);
        Assert.Null((result as Vision.Core.Results.IOutputImageResult)?.OutputImage);
    }

    [Fact]
    public void MissingFile_RunReturnsFailureWithoutNativeImage()
    {
        // 仅验证真实工具的无文件失败路径；不是HALCON ReadImage正向运行的证据。
        using var tool = new ImageFileTool();
        tool.Parameters.FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        var result = tool.RunImage(null);
        Assert.False(result.Success); Assert.Contains("图像文件不存在", result.Message);
        Assert.Null(tool.LoadedImage);
    }
}
