using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

public sealed class BlobBrowserSearchRegionTests
{
    [Fact]
    public void FreshBrowser_HasNoRoi_AndBlobUsesWholeInputWithoutClearingASeed()
    {
        var tool = new BlobTool(); tool.Parameters.MinGray = 128; tool.Parameters.Features = [];
        var image = VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 400).ToArray());
        ToolEntry entry = new("Regions", "Test", () =>
            (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null));
        using var workspace = new VisionBrowserWorkspace(new([entry], [], _ => throw new InvalidOperationException(), () => image));
        using var browser = new VisionBrowserViewModel(workspace);
        Assert.Empty(browser.RoiEditor.Rois); Assert.Null(browser.RoiEditor.Selected);
        browser.LoadImage(); browser.CurrentCard!.Run();
        Assert.Equal(400, Assert.Single(Assert.IsType<BlobResult>(browser.CurrentCard.LastResult).Items).Area);
        Assert.Empty(browser.RoiEditor.Rois);
    }
    [Fact]
    public async Task BackgroundRun_AppliesMinAreaThroughPreparedSnapshot()
    {
        var tool = new BlobTool(); tool.Parameters.MinGray = 128; tool.Parameters.MinArea = 401;
        using var card = new ToolCardViewModel(new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool));
        card.SetImage(VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 400).ToArray()));
        await card.RunAsync();
        Assert.Empty(Assert.IsType<BlobResult>(card.LastResult).Items);
    }

    [Fact]
    public async Task StandaloneCard_PreservesToolConfiguredDomain_WhenNoBrowserScopeSourceWasProvided()
    {
        var tool = new BlobTool { SearchRegion = new(Vision.Core.Roi.Roi.Rectangle(5, 5, 4, 4)) };
        tool.Parameters.Features = [];
        using var card = new ToolCardViewModel(new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool));
        card.SetImage(VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, new byte[400]));
        await card.RunAsync();
        Assert.Equal(16, Assert.Single(Assert.IsType<BlobResult>(card.LastResult).Items).Area);
    }
    [Fact]
    public void BlobSearchRegion_KeepsOnlyOneRoi_AndNewAddsReplaceTheOldOne()
    {
        var tool = new BlobTool(); tool.Parameters.MinGray = 128; tool.Parameters.Features = [];
        var image = VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 400).ToArray());
        ToolEntry entry = new("Regions", "Test", () =>
            (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null));
        using var workspace = new VisionBrowserWorkspace(new([entry], [], _ => throw new InvalidOperationException(), () => image));
        using var browser = new VisionBrowserViewModel(workspace);
        browser.LoadImage();

        browser.RoiEditor.AddRectangle(5, 5, 4, 4);
        browser.RoiEditor.AddRectangle(10, 10, 6, 6);

        var roi = Assert.Single(browser.RoiEditor.Rois);
        Assert.Same(roi, browser.RoiEditor.Selected);
        Assert.Equal(new Pose2D(10, 10, 0), roi.Pose);
        Assert.Equal(new Size2D(6, 6), roi.Size);

        browser.CurrentCard!.Run();
        Assert.Equal(36, Assert.Single(Assert.IsType<BlobResult>(browser.CurrentCard.LastResult).Items).Area);
    }
    [Fact]
    public void SelectedRoiDrivesBlob_HidingLayerDoesNotDisableDomain_EditingInvalidatesResults()
    {
        var tool = new BlobTool(); tool.Parameters.MinGray = 128; tool.Parameters.Features = [];
        var pixels = Enumerable.Repeat((byte)255, 20 * 20).ToArray();
        var image = VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, pixels);
        ToolEntry entry = new("NotNamedBlob", "NotNamedOpenCV", () =>
            (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null));
        using var workspace = new VisionBrowserWorkspace(new([entry], [], _ => throw new InvalidOperationException(), () => image));
        using var browser = new VisionBrowserViewModel(workspace);
        browser.LoadImage(); browser.RoiEditor.Clear(); browser.RoiEditor.AddRectangle(5, 5, 4, 4);
        Assert.True(browser.SupportsSearchRegion); // 显式能力，不判断目录名称/Backend。
        browser.CurrentCard!.Run();
        Assert.Equal(16, Assert.Single(Assert.IsType<BlobResult>(browser.CurrentCard.LastResult).Items).Area);
        var result = browser.CurrentCard.LastResult;
        browser.ShowRois = false;
        Assert.Same(result, browser.CurrentCard.LastResult);
        browser.CurrentCard.Run();
        Assert.Equal(16, Assert.Single(Assert.IsType<BlobResult>(browser.CurrentCard.LastResult).Items).Area);
        browser.RoiEditor.SelectedWidth = 6;
        Assert.Null(browser.CurrentCard.LastResult);
        browser.CurrentCard.Run();
        Assert.Equal(24, Assert.Single(Assert.IsType<BlobResult>(browser.CurrentCard.LastResult).Items).Area);
        browser.RoiEditor.Selected = null; // 有 ROI 却未选中，不得静默退回全图。
        browser.CurrentCard.Run();
        Assert.Null(browser.CurrentCard.LastResult); Assert.Contains("请选择一个搜索 ROI", browser.CurrentCard.StatusText);
        browser.RoiEditor.Clear(); browser.CurrentCard.Run();
        Assert.Equal(400, Assert.Single(Assert.IsType<BlobResult>(browser.CurrentCard.LastResult).Items).Area);
    }
}
