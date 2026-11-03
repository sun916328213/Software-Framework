using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

public sealed class ResultSelectionTests
{
    [Fact]
    public void HitTesting_UsesActualShapeAndHoles_NotBounds_AndSupportsSinglePoint()
    {
        using var card = new ToolCardViewModel(new ResultTool(CreateResult));
        card.Run();
        var results = card.Results;
        Assert.True(results.SelectAt(new(15, 30), 0.1));
        Assert.Equal(9, results.SelectedId);
        Assert.False(results.SelectAt(new(30, 30), 0.1)); // 孔洞内部不命中。
        Assert.Null(results.SelectedId);
        Assert.True(results.SelectAt(new(65, 35), 0.1));
        Assert.Equal(4, results.SelectedId);
        Assert.False(results.SelectAt(new(85, 35), 0.1)); // 在 L 形外接框里，但不在真实区域里。
        Assert.True(results.SelectAt(new(85, 60), 0.1));
        Assert.Equal(7, results.SelectedId);
    }

    [Fact]
    public void ReexecutionFailureAndDisposal_ClearRowsAndSelection_RejectOldRowsWithReusedId()
    {
        var failed = false;
        using var card = new ToolCardViewModel(new ResultTool(() => failed ? new BlobResult { Success = false } : CreateResult()));
        card.Run();
        var results = card.Results;
        results.SelectId(9);
        var oldRow = results.SelectedItem;
        card.Run();
        Assert.Same(results, card.Results);
        Assert.Null(results.SelectedId);
        results.SelectedItem = oldRow;
        Assert.Null(results.SelectedId);
        results.SelectId(9);
        Assert.NotSame(oldRow, results.SelectedItem);
        failed = true;
        card.Run();
        Assert.Empty(results.Items);
        Assert.Null(results.SelectedId);
        failed = false;
        card.Run();
        results.SelectId(4);
        card.SetImage(null);
        Assert.Empty(results.Items);
        Assert.Null(results.SelectedId);
        card.Run();
        results.SelectId(9);
        card.Dispose();
        Assert.Empty(results.Items);
        Assert.Null(results.Result);
        results.SelectedItem = oldRow;
        Assert.Null(results.SelectedId);
    }

    [Fact]
    public void ParameterImageAndToolChanges_InvalidateSharedSelection_WithoutBorrowerOwningSession()
    {
        using var workspace = new VisionBrowserWorkspace(new(
            [new("First", "Test", () => (new ResultTool(CreateResult), new object(), null)),
             new("Second", "Test", () => (new ResultTool(CreateResult), new object(), null))], [],
            _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
        using var first = new VisionBrowserViewModel(workspace);
        using var borrowed = new VisionBrowserViewModel(workspace);
        first.CurrentCard!.Run();
        var results = first.CurrentCard.Results;
        Assert.Same(results, borrowed.CurrentCard!.Results);
        results.SelectId(9);
        borrowed.Dispose();
        Assert.Equal(9, results.SelectedId);
        first.ParametersChanged();
        Assert.Null(results.SelectedId);
        Assert.Empty(results.Items);
        first.CurrentCard.Run();
        results.SelectId(4);
        first.LoadImage();
        Assert.Null(results.SelectedId);
        first.CurrentCard.Run();
        results.SelectId(7);
        first.SelectedEntry = first.ToolEntries[1];
        Assert.Empty(results.Items);
        Assert.Null(results.SelectedId);
        Assert.NotSame(results, first.CurrentCard!.Results);
    }

    internal static BlobResult CreateResult() => new()
    {
        Items =
        [
            new() { Index = 9, Area = 1200, Center = new(30, 30), Bounds = new(10, 10, 41, 41),
                Contours = [Path(false, (10,10), (50,10), (50,50), (10,50)), Path(true, (20,20), (40,20), (40,40), (20,40))] },
            new() { Index = 4, Area = 500, Center = new(70, 20), Bounds = new(60, 10, 31, 31),
                Contours = [Path(false, (60,10), (90,10), (90,20), (70,20), (70,40), (60,40))] },
            new() { Index = 7, Area = 1, Center = new(85, 60), Bounds = new(85, 60, 1, 1),
                Contours = [Path(false, (85,60))] }
        ]
    };
    private static RegionContour Path(bool hole, params (double X, double Y)[] points) =>
        new(points.Select(p => new Point2D(p.X, p.Y)).ToArray(), hole);

    internal sealed class ResultTool(Func<ToolResult> run) : IExecutableTool
    {
        public string Name => "Synthetic result";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => run();
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
}
