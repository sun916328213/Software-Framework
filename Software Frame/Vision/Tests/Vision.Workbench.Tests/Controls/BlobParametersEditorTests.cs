using System.Text.Json;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Cv = Vision.Tools.OpenCv.Inspection;
using Halcon = Vision.Tools.Halcon.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Trait("Category", "Unit")]
public sealed class BlobParametersEditorTests
{
    [Fact]
    public void OpenCvEditor_WritesScalarParameters_AndPreservesOtherRangesWhenSelectingFeatures()
    {
        var parameters = new Cv.BlobParameters { MinGray = 128, Features = [new() { Feature = "area", Min = 12, Max = 500 }] };
        var changes = 0;
        using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => changes++);
        Assert.NotNull(editor);
        editor.MinGray = 130;
        editor.MaxGray = 240;
        editor.MinArea = 50;
        editor.Relation = BlobFilterRelation.Or;
        editor.SelectedFeatures.Add("width");
        Assert.Equal(130, parameters.MinGray);
        Assert.Equal(240, parameters.MaxGray);
        Assert.Equal(50, parameters.MinArea);
        Assert.Equal(Cv.BlobFeatureLogic.Or, parameters.Logic);
        var area = parameters.Features.Single(f => f.Feature == "area");
        Assert.Equal(12, area.Min);
        Assert.Equal(500, area.Max);
        var width = editor.Ranges.Single(r => r.Feature == "width");
        width.Min = 5;
        width.Max = 40;
        Assert.Equal(40, parameters.Features.Single(f => f.Feature == "width").Max);
        editor.SelectedFeatures.Remove("width");
        Assert.Equal(12, Assert.Single(parameters.Features).Min);
        Assert.Equal(8, changes);
        Assert.Equal(Cv.BlobTool.SupportedFeatures, editor.AvailableFeatures);
        Assert.Equal(10, editor.AvailableFeatures.Count);
    }

    [Fact]
    public void NewBlobs_StartWithoutAnyFeatureSelected()
    {
        var openCv = new Cv.BlobParameters();
        using var openCvEditor = BlobParametersEditorViewModel.TryCreate(openCv, () => { });
        Assert.NotNull(openCvEditor);
        Assert.Empty(openCvEditor.SelectedFeatures);
        Assert.Empty(openCvEditor.Ranges);
        Assert.Empty(openCv.Features);
        Assert.Equal(10, openCvEditor.MinArea);

        var halcon = new Halcon.BlobParameters();
        using var halconEditor = BlobParametersEditorViewModel.TryCreate(halcon, () => { });
        Assert.NotNull(halconEditor);
        Assert.Empty(halconEditor.SelectedFeatures);
        Assert.Empty(halconEditor.Ranges);
        Assert.Empty(halcon.Features);
        Assert.Equal(10, halconEditor.MinArea);
    }

    [Fact]
    public void SelectingPhi_DefaultsToRadiansCoveringBothSigns_WithoutChangingExistingRanges()
    {
        var cv = new Cv.BlobParameters { Features = [new() { Feature = "unknown", Min = -8, Max = 3 }] };
        var halcon = new Halcon.BlobParameters { Features = [new() { Feature = "phi", Min = 0.2, Max = 0.4 }] };
        using var cvEditor = BlobParametersEditorViewModel.TryCreate(cv, () => { });
        using var halconEditor = BlobParametersEditorViewModel.TryCreate(halcon, () => { });
        Assert.NotNull(cvEditor);
        Assert.NotNull(halconEditor);
        cvEditor.SelectedFeatures.Add("phi");
        var phi = cv.Features.Single(f => f.Feature == "phi");
        Assert.True(phi.Min <= -Math.PI / 2);
        Assert.True(phi.Max >= Math.PI / 2);
        Assert.Equal(-8, cv.Features.Single(f => f.Feature == "unknown").Min);
        Assert.Equal(0.2, Assert.Single(halcon.Features).Min);
        Assert.Equal  (0.4, halcon.Features[0].Max);
        halconEditor.SelectedFeatures.Remove("phi");
        halconEditor.SelectedFeatures.Add("phi");
        Assert.True(Assert.Single(halcon.Features).Min <= -Math.PI / 2);
        Assert.True(halcon.Features[0].Max >= Math.PI / 2);
        var description = (Vision.Core.ToolPropertyAttribute)Attribute.GetCustomAttribute(
            typeof(BlobParametersEditorViewModel).GetProperty(nameof(BlobParametersEditorViewModel.Ranges))!,
            typeof(Vision.Core.ToolPropertyAttribute))!;
        Assert.Contains("phi", description.Description);
        Assert.Contains("弧度", description.Description);
    }

    [Fact]
    public void RangeTableVisibility_FollowsFeatureSelection()
    {
        var parameters = new Cv.BlobParameters();
        using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => { });
        Assert.NotNull(editor);
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.False(editor.HasSelectedFeatures);

        editor.SelectedFeatures.Add("area");
        Assert.True(editor.HasSelectedFeatures);
        Assert.Contains(nameof(editor.HasSelectedFeatures), raised);

        editor.SelectedFeatures.Clear();
        Assert.False(editor.HasSelectedFeatures);
        Assert.Equal(2, raised.Count(name => name == nameof(editor.HasSelectedFeatures)));
    }

    [Fact]
    public void HalconEditor_SharesBlobFeatureList_WithoutInvokingSdk()
    {
        var parameters = new Halcon.BlobParameters();
        using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => { });
        Assert.NotNull(editor);
        // 两个后端的多选项必须完全一致，避免再次出现可勾选特征不对齐。
        Assert.Equal(Cv.BlobTool.SupportedFeatures, editor.AvailableFeatures);
        editor.MinGray = 90;
        editor.Relation = BlobFilterRelation.Or;
        editor.SelectedFeatures.Add("circularity");
        editor.Ranges.Single(r => r.Feature == "circularity").Min = 0.7;
        Assert.Equal(90, parameters.MinGray);
        Assert.Equal(Halcon.BlobFeatureLogic.Or, parameters.Logic);
        Assert.Equal(0.7, parameters.Features.Single(f => f.Feature == "circularity").Min);
    }

    [Fact]
    public void OpeningEditor_DoesNotRewriteConfiguration_AndKeepsUnknownAndDuplicateFeatures()
    {
        var parameters = new Cv.BlobParameters
        {
            Features = [new() { Feature = "unknown", Min = 7, Max = 8 }, new() { Feature = "area", Min = 3 }, new() { Feature = "area", Min = 9 }]
        };
        var before = JsonSerializer.Serialize(parameters);
        var changes = 0;
        using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => changes++);
        Assert.NotNull(editor);
        Assert.Equal(before, JsonSerializer.Serialize(parameters));
        Assert.Equal(0, changes);
        Assert.Equal(3, editor.Ranges.Count);
        Assert.Contains("unknown", editor.AvailableFeatures);
        Assert.Contains("unknown", editor.SelectedFeatures);
        editor.SelectedFeatures.Add("width");
        Assert.Equal(2, parameters.Features.Count(f => f.Feature == "area"));
        Assert.Equal(7, parameters.Features.Single(f => f.Feature == "unknown").Min);
        editor.SelectedFeatures.Remove("unknown");
        Assert.DoesNotContain("unknown", parameters.Features.Select(f => f.Feature));
        Assert.DoesNotContain("unknown", editor.SelectedFeatures);
    }

    [Fact]
    public void InvalidCollection_UsesCompatibilityPath_WithoutRepairingOrThrowing()
    {
        var parameters = new Cv.BlobParameters { Features = null! };
        Assert.Null(BlobParametersEditorViewModel.TryCreate(parameters, () => { }));
        Assert.Null(parameters.Features);
        Assert.Null(BlobParametersEditorViewModel.TryCreate(new object(), () => { }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void InvalidGrayValue_IsRejectedWithoutWritingOrRepairingConfiguration(int value)
    {
        var parameters = new Cv.BlobParameters { MinGray = 128 };
        var changes = 0;
        using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => changes++);
        Assert.NotNull(editor);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.MinGray = value);
        Assert.Equal(128, parameters.MinGray);
        Assert.Equal(0, changes);
        parameters.MinGray = value;
        Assert.Null(BlobParametersEditorViewModel.TryCreate(parameters, () => changes++));
        Assert.Equal(value, parameters.MinGray);
    }

    [Fact]
    public void NegativeMinArea_IsRejectedWithoutWriting()
    {
        var parameters = new Cv.BlobParameters { MinArea = 5 };
        var changes = 0;
        using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => changes++);
        Assert.NotNull(editor);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.MinArea = -1);
        Assert.Equal(5, parameters.MinArea);
        Assert.Equal(0, changes);
        parameters.MinArea = -1;
        Assert.Null(BlobParametersEditorViewModel.TryCreate(parameters, () => changes++));
    }

    [Fact]
    public void EditingMinArea_FiltersRealBlobExecution_AndInvalidatesOldResult()
    {
        using var browser = new TestBrowserViewModel();
        var editor = Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor);
        browser.LoadImage();
        browser.CurrentCard!.Run();
        Assert.Equal(400, Assert.Single(Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult).Items).Area);

        editor.MinArea = 401;
        Assert.Null(browser.CurrentCard.LastResult);
        browser.CurrentCard.Run();
        Assert.Empty(Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult).Items);

        editor.MinArea = 400;
        browser.CurrentCard.Run();
        Assert.Single(Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult).Items);
    }

    [Fact]
    public void EditingRange_ChangesRealBlobExecution_AndInvalidRangeClearsOldResult()
    {
        using var browser = new TestBrowserViewModel();
        var editor = Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor);
        browser.LoadImage();
        browser.CurrentCard!.Run();
        Assert.Equal(400, Assert.Single(Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult).Items).Area);
        editor.SelectedFeatures.Add("area");
        var area = Assert.Single(editor.Ranges);
        area.Min = 401;
        Assert.Null(browser.CurrentCard.LastResult);
        browser.CurrentCard.Run();
        var filtered = Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult);
        Assert.True(filtered.Success, filtered.Message);
        Assert.Empty(filtered.Items);
        area.Max = 400;
        Assert.Null(browser.CurrentCard.LastResult);
        Assert.False(browser.CurrentCard.IsValid);
        Assert.Contains("校验失败", browser.CurrentCard.StatusText);
        area.Min = 0;
        browser.CurrentCard.Run();
        Assert.Single(Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult).Items);
    }

    [Fact]
    public void SwitchingTool_StopsOldEditorFromWritingParameters_AndDetachesRangeSubscriptions()
    {
        using var browser = new TestBrowserViewModel();
        var parameters = Assert.IsType<Cv.BlobParameters>(browser.CurrentParameters);
        var editor = Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor);
        editor.SelectedFeatures.Add("area");
        var oldRow = Assert.Single(editor.Ranges);
        browser.SelectedEntry = browser.ToolEntries.Single(t => t.Name == "OpenCV/Threshold");
        Assert.Null(browser.BlobEditor);
        Assert.Throws<ObjectDisposedException>(() => editor.MinGray = 50);
        oldRow.Min = 123;
        editor.SelectedFeatures.Add("width");
        Assert.Equal(128, parameters.MinGray);
        Assert.Equal(0, Assert.Single(parameters.Features).Min);
        Assert.Equal("校验通过", browser.CurrentCard!.StatusText);
    }

    [Fact]
    public void BorrowedViewModelDisposal_DoesNotCloseSharedEditor_WorkspaceDisposalDoes()
    {
        var parameters = new Cv.BlobParameters();
        using var workspace = new VisionBrowserWorkspace(new(
            [new("Blob", "Test", () => (new OpenCvToolCardAdapter<Cv.BlobParameters, Cv.BlobResult>(new Cv.BlobTool { Parameters = parameters }), parameters, null))],
            [], _ => throw new InvalidOperationException(), () => null));
        using var first = new VisionBrowserViewModel(workspace);
        using var second = new VisionBrowserViewModel(workspace);
        var editor = Assert.IsType<BlobParametersEditorViewModel>(first.BlobEditor);
        Assert.Same(editor, second.BlobEditor);
        first.Dispose();
        editor.MinGray = 100;
        Assert.Equal(100, parameters.MinGray);
        workspace.Dispose();
        workspace.Dispose();
        Assert.Null(second.BlobEditor);
        Assert.Throws<ObjectDisposedException>(() => editor.MaxGray = 200);
    }
}
