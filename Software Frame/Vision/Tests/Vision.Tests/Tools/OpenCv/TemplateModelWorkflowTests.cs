using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Tools.OpenCv.Matching;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class TemplateModelWorkflowTests
{
    [Fact]
    public void CaptureSaveClearLoadAndMatch_PreservePixelsAndRuntimeParameters_WithoutFakeTraining()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "sample.vtemplate");
            using var tool = new TemplateMatchTool();
            ITemplateTool editor = tool;
            byte[] pixels = [0, 255, 0, 255];
            editor.SetTemplate(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, pixels));
            pixels[0] = 255;
            Assert.Equal(0, editor.TemplateImage!.Pixels[0]);
            Assert.False(editor.RequiresTraining);
            Assert.Throws<NotSupportedException>(() => editor.Train());
            tool.Parameters.MinScore = 0.95;
            tool.Parameters.Method = TemplateMatchMethod.SqDiffNormed;
            editor.SaveModel(path);
            editor.ClearModel();
            Assert.False(editor.HasModel);
            editor.LoadModel(path);
            Assert.True(editor.HasModel);
            Assert.Equal(0.95, tool.Parameters.MinScore);
            Assert.Equal(TemplateMatchMethod.SqDiffNormed, tool.Parameters.Method);
            var search = VisionImage.FromBuffer(3, 3, PixelFormat.Mono8, [0, 0, 0, 0, 0, 255, 0, 0, 255]);
            using var result = tool.Run(search);
            Assert.True(result.Success, result.Message);
            var match = Assert.Single(result.Matches);
            Assert.Equal(1, match.Bounds.X);
            Assert.Equal(1, match.Bounds.Y);
            Assert.True(match.Score > 0.99);
            var original = editor.TemplateImage;
            File.WriteAllText(path, "{\"Version\":99,\"Kind\":\"opencv-gray-template\"}");
            Assert.Throws<InvalidDataException>(() => editor.LoadModel(path));
            Assert.Same(original, editor.TemplateImage);
            tool.Dispose();
            Assert.Throws<ObjectDisposedException>(() => editor.SetTemplate(search));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void RejectedTemplateCapture_PreservesPreviouslyAcceptedTemplate()
    {
        using var tool = new TemplateMatchTool();
        ITemplateTool editor = tool;
        editor.SetTemplate(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 99, 3, 4, 99], stride: 3));
        var original = editor.TemplateImage;
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, original!.Pixels);
        Assert.Throws<ArgumentNullException>(() => editor.SetTemplate(null!));
        Assert.Throws<ArgumentException>(() => editor.SetTemplate(VisionImage.FromBuffer(1, 1, PixelFormat.Bgr24, [1, 2, 3])));
        Assert.Throws<ArgumentException>(() => editor.SetTemplate(new VisionImage { Width = 2, Height = 2, Format = PixelFormat.Mono8, Pixels = [1] }));
        Assert.Same(original, editor.TemplateImage);
        Assert.True(editor.HasModel);
    }

    [Fact]
    public void FailedSerialization_DoesNotOverwriteExistingModelFile_OrLeaveTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "existing.json");
            File.WriteAllText(path, "original");
            Assert.Throws<ArgumentException>(() => TemplateModelFile.Save(path, new { Number = double.NaN }));
            Assert.Equal("original", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
