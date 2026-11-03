using System.Text.Json;
using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tools.OpenCv.Inspection;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class BlobRoiImageTests
{
    [Fact]
    public void IntermediateImage_IsActualThresholdAndDomain_BeforeFeatureFiltering()
    {
        var input = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]);
        input.FrameId = 123; input.Timestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        using var tool = new BlobTool { SearchRegion = new(Roi.Rectangle(4, 4, 4, 4)) };
        tool.Parameters.MinGray = 0; tool.Parameters.MaxGray = 0;
        tool.Parameters.Features = [new() { Feature = "area", Min = 100, Max = 1000 }];
        using var result = tool.Run(input);
        Assert.True(result.Success, result.Message); Assert.Empty(result.Items);
        var stage = Assert.IsType<VisionImage>(result.RoiImage);
        Assert.Equal(8, stage.Width); Assert.Equal(8, stage.Height); Assert.Equal(PixelFormat.Mono8, stage.Format);
        Assert.Equal(123, stage.FrameId); Assert.Equal(input.Timestamp, stage.Timestamp);
        Assert.NotSame(input.Pixels, stage.Pixels);
        for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++)
            Assert.Equal(x >= 2 && x < 6 && y >= 2 && y < 6 ? (byte)255 : (byte)0, stage.Pixels[y * 8 + x]);
        Assert.All(input.Pixels, pixel => Assert.Equal((byte)0, pixel));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.False(json.RootElement.TryGetProperty(nameof(result.RoiImage), out _));
        using var interfaceJson = JsonDocument.Parse(JsonSerializer.Serialize<Vision.Core.Results.IRoiImageResult>(result));
        Assert.Empty(interfaceJson.RootElement.EnumerateObject());
        tool.Dispose(); Assert.Equal(16, stage.Pixels.Count(pixel => pixel == 255));
        result.Dispose(); Assert.Null(result.RoiImage); Assert.Equal(16, stage.Pixels.Count(pixel => pixel == 255));
    }
    [Fact]
    public void FullAndEmptyDomains_ProduceIndependentFullSizeIntermediateImages()
    {
        var input = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]);
        using var tool = new BlobTool(); tool.Parameters.MinGray = 0; tool.Parameters.MaxGray = 0; tool.Parameters.Features = [];
        using var full = tool.Run(input);
        Assert.True(full.Success, full.Message); var first = Assert.IsType<VisionImage>(full.RoiImage);
        Assert.All(first.Pixels, pixel => Assert.Equal((byte)255, pixel));
        tool.SearchRegion = new(Roi.Circle(100, 100, 4));
        using var empty = tool.Run(input);
        Assert.True(empty.Success, empty.Message); Assert.Empty(empty.Items);
        var second = Assert.IsType<VisionImage>(empty.RoiImage);
        Assert.Equal(8, second.Width); Assert.Equal(8, second.Height); Assert.All(second.Pixels, pixel => Assert.Equal((byte)0, pixel));
        Assert.NotSame(first.Pixels, second.Pixels); Assert.All(first.Pixels, pixel => Assert.Equal((byte)255, pixel));
    }
    [Fact]
    public async Task PreparedRun_IntermediateImageUsesTheCapturedInput_NotTheEditedOriginal()
    {
        var input = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]);
        var tool = new BlobTool { SearchRegion = new(Roi.Rectangle(4, 4, 4, 4)) };
        tool.Parameters.MinGray = 0; tool.Parameters.MaxGray = 0; tool.Parameters.Features = [];
        var run = tool.PrepareRun(input);
        Array.Fill(input.Pixels, (byte)255); tool.SearchRegion = null; tool.Dispose();
        using var result = Assert.IsType<BlobResult>(await Task.Run(run));
        Assert.True(result.Success, result.Message);
        var stage = Assert.IsType<VisionImage>(result.RoiImage);
        Assert.Equal(16, stage.Pixels.Count(pixel => pixel == 255));
        Assert.Equal((byte)0, stage.Pixels[0]); Assert.Equal((byte)255, stage.Pixels[3 * 8 + 3]);
    }
}
