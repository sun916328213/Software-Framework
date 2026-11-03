using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Matching;

namespace Vision.Workbench.Tests.Controls;

public sealed class VisionDebugWorkflowTests
{
    [Fact]
    public void DefaultBlob_DemoProducesOne400PixelRegion_AndInputChangesInvalidateResult()
    {
        using var browser = new TestBrowserViewModel();
        Assert.Equal("OpenCV/Blob", browser.SelectedEntry?.Name);
        browser.LoadImage();
        Assert.NotNull(browser.InputImage);
        browser.CurrentCard!.Run();
        var result = Assert.IsType<BlobResult>(browser.CurrentCard.LastResult);
        Assert.True(result.Success, result.Message);
        var blob = Assert.Single(result.Items);
        Assert.Equal(400, blob.Area);
        Assert.Equal(10, blob.Bounds.X);
        Assert.Equal(20, blob.Bounds.Width);
        Assert.NotEmpty(blob.Contours);
        var row = Assert.Single(browser.CurrentCard.Results.Items);
        Assert.Equal(blob.Index, row.Id);
        browser.CurrentCard.Results.SelectId(row.Id);
        Assert.Equal(row.Id, browser.CurrentCard.Results.SelectedId);
        Assert.Contains("400", browser.CurrentCard.ResultDetails);
        browser.LoadImage();
        Assert.Null(browser.CurrentCard.LastResult);
        Assert.Null(browser.CurrentCard.Results.SelectedId);
        Assert.Empty(browser.CurrentCard.Results.Items);
        browser.CurrentCard.Run();
        browser.ParametersChanged();
        Assert.Null(browser.CurrentCard.LastResult);
    }

    [Fact]
    public void RoiTemplate_IsIndependentOfSearchImage_AndCanActuallyMatch()
    {
        using var browser = new TestBrowserViewModel();
        browser.SelectedEntry = browser.ToolEntries.Single(t => t.Name == "OpenCV/TemplateMatch");
        browser.LoadImage();
        var search = browser.InputImage;
        browser.RoiEditor.Clear();
        browser.RoiEditor.AddRectangle(20, 20, 30, 30);
        Assert.True(browser.SetTemplateFromRoiCommand.CanExecute(null));
        browser.SetTemplateFromRoi();
        Assert.Same(search, browser.InputImage);
        var parameters = Assert.IsType<TemplateMatchParameters>(browser.CurrentParameters);
        Assert.Equal(30, parameters.Template!.Width);
        Assert.NotSame(search!.Pixels, parameters.Template.Pixels);
        browser.CurrentCard!.Run();
        var result = Assert.IsType<TemplateMatchResult>(browser.CurrentCard.LastResult);
        Assert.True(result.Success, result.Message);
        var match = Assert.Single(result.Matches);
        Assert.True(match.Score > 0.99);
        Assert.Equal(20, match.Center.X);
        Assert.Equal(20, match.Center.Y);
        browser.Dispose();
        Assert.False(browser.SetTemplateFromRoiCommand.CanExecute(null));
        Assert.False(browser.CropInputCommand.CanExecute(null));
    }

    [Fact]
    public void Crop_ClipsToImage_RespectsPaddedStride_AndRejectsNonRectangles()
    {
        var image = VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, [1, 2, 3, 99, 4, 5, 6, 99], stride: 4);
        var crop = VisionImageCrop.Crop(image, Roi.Rectangle(2.5, 1, 3, 2));
        Assert.Equal(2, crop.Width);
        Assert.Equal(2, crop.Height);
        Assert.Equal(new byte[] { 2, 3, 5, 6 }, crop.Pixels);
        Assert.Throws<InvalidOperationException>(() => VisionImageCrop.Crop(image, Roi.Circle(1, 1, 2)));
        Assert.Throws<InvalidOperationException>(() => VisionImageCrop.Crop(image, Roi.Rectangle(100, 100, 5, 5)));
    }

    [Fact]
    public void CropInput_ReplacesInputAndClearsOldResultAndAnnotations()
    {
        using var browser = new TestBrowserViewModel();
        browser.LoadImage();
        browser.CurrentCard!.Run();
        browser.RoiEditor.Clear();
        browser.RoiEditor.AddRectangle(20, 20, 20, 20);
        browser.CropInput();
        Assert.Equal(20, browser.InputImage!.Width);
        Assert.All(browser.InputImage.Pixels, p => Assert.Equal(255, p));
        Assert.Null(browser.CurrentCard.LastResult);
        Assert.Equal(0, browser.RoiEditor.Count);
        Assert.Contains("局部", browser.ImageInfoText);
    }

    [Fact]
    public void FailedExecution_PublishesFailure_WithoutRepeatingToolRun()
    {
        var tool = new FailureTool();
        using var workspace = new VisionBrowserWorkspace(new(
            [new("Failure", "Test", () => (tool, new object(), null))], [],
            _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
        workspace.LoadImage();
        workspace.Model.CurrentCard!.Run();
        Assert.Equal(1, tool.RunCount);
        Assert.False(workspace.Model.CurrentCard.LastResult!.Success);
        Assert.Equal("expected failure", workspace.Model.CurrentCard.LastResult.Message);
        Assert.Contains("失败", workspace.Model.CurrentCard.StatusText);
    }

    [Fact]
    public void ResultOwnership_ReplacingInputAndClosingCardDisposesEachResultOnce()
    {
        var tool = new ResultTool();
        using var card = new ToolCardViewModel(tool);
        card.Run();
        var first = Assert.IsType<TrackedResult>(card.LastResult);
        card.Run();
        Assert.Equal(1, first.DisposeCount);
        var second = Assert.IsType<TrackedResult>(card.LastResult);
        card.SetImage(null);
        Assert.Equal(1, second.DisposeCount);
        card.Run();
        var third = Assert.IsType<TrackedResult>(card.LastResult);
        card.Dispose();
        card.Dispose();
        Assert.Equal(1, third.DisposeCount);
    }

    private sealed class FailureTool : IExecutableTool
    {
        public int RunCount { get; private set; }
        public string Name => "Failure";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => ValidationResult.Success();
        public ToolResult Run(VisionImage? image) { RunCount++; return new() { Success = false, Message = "expected failure" }; }
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private sealed class ResultTool : IExecutableTool
    {
        public string Name => "Result";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => ValidationResult.Success();
        public ToolResult Run(VisionImage? image) => new TrackedResult();
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private sealed class TrackedResult : ToolResult
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }
}
