using System.IO;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;


namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchProjectTests(WorkbenchAppFixture host)
{
    [Fact]
    public void ExportAndImport_RestoresToolParametersRoisViewAndImage()
    {
        host.Invoke(() =>
        {
            var catalog = Catalog();
            using var source = new VisionBrowserWorkspace(catalog);
            source.Model.SelectedEntry = source.Model.ToolEntries.Single(entry => entry.Name == "OpenCV/Threshold");
            source.LoadImage();
            source.Model.InputImage!.Source = Path.Combine(Path.GetTempPath(), "workbench-demo.png");
            Assert.IsType<ThresholdParameters>(source.Model.CurrentParameters).MaxGray = 123;
            source.RoiEditor.AddRectangle(20, 20, 30, 10);
            source.RoiEditor.AddCircle(50, 40, 8);
            source.Model.SelectedImageView = ImageViewKind.Result;
            source.Model.ShowResultBounds = true;
            var json = source.ExportProjectJson();

            using var target = new VisionBrowserWorkspace(catalog);
            target.ImportProjectJson(json);

            Assert.Equal("OpenCV/Threshold", target.Model.SelectedEntry!.Name);
            Assert.Equal(123, Assert.IsType<ThresholdParameters>(target.Model.CurrentParameters).MaxGray);
            Assert.Equal(2, target.RoiEditor.Count);
            Assert.Equal(RoiKind.Circle, target.RoiEditor.Selected!.Kind);
            Assert.Equal(8, target.RoiEditor.Selected.Size.Width);
            Assert.Equal(ImageViewKind.Result, target.Model.SelectedImageView);
            Assert.True(target.Model.ShowResultBounds);
            Assert.Equal("restored", target.Model.InputImage!.Source);
            Assert.Equal(80, target.Model.InputImage.Width);
            Assert.Null(target.Model.BlobEditor);
        });
    }

    [Fact]
    public void ExportAndImport_RestoresBlobFeaturesAndRebuildsTheEditor()
    {
        host.Invoke(() =>
        {
            var catalog = Catalog();
            using var source = new VisionBrowserWorkspace(catalog);
            source.Model.SelectedEntry = source.Model.ToolEntries.Single(entry => entry.Name == "OpenCV/Blob");
            var parameters = Assert.IsType<BlobParameters>(source.Model.CurrentParameters);
            parameters.MinArea = 42;
            parameters.Features = [new BlobFeatureRange { Feature = "area", Min = 5, Max = 500 }];
            var json = source.ExportProjectJson();

            using var target = new VisionBrowserWorkspace(catalog);
            target.ImportProjectJson(json);

            var imported = Assert.IsType<BlobParameters>(target.Model.CurrentParameters);
            Assert.Equal(42, imported.MinArea);
            var editor = Assert.IsType<Vision.Controls.ViewModels.BlobParametersEditorViewModel>(target.Model.BlobEditor);
            Assert.Equal(42, editor.MinArea);
            Assert.Contains("area", editor.SelectedFeatures);
            Assert.Equal(5, Assert.Single(editor.Ranges).Min);
            Assert.Equal(500, editor.Ranges[0].Max);
        });
    }

    [Fact]
    public void OpenCvFindLine_NewCalipersRoundTrip_AndLegacyMissingModeRemainsCanny()
    {
        host.Invoke(() =>
        {
            using var source = new VisionBrowserWorkspace(Catalog());
            source.Model.SelectedEntry = source.Model.ToolEntries.Single(entry => entry.Name == "OpenCV/FindLine");
            var parameters = Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(source.Model.CurrentParameters);
            Assert.Equal(Vision.Core.Geometry.LineSearchKind.Calipers, parameters.SearchMode);
            parameters.CaliperCount = 15; parameters.CaliperHalfWidth = 2.75; parameters.Threshold = 12.5;
            parameters.Sigma = 1.2; parameters.EdgeSelection = Vision.Tools.OpenCv.Measurement.CaliperEdgeSelection.Last;
            parameters.Transition = Vision.Tools.OpenCv.Measurement.EdgeTransition.Negative;
            using var restored = new VisionBrowserWorkspace(Catalog());
            restored.ImportProjectJson(source.ExportProjectJson());
            var actual = Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(restored.Model.CurrentParameters);
            Assert.Equal(Vision.Core.Geometry.LineSearchKind.Calipers, actual.SearchMode);
            Assert.Equal(15, actual.CaliperCount); Assert.Equal(2.75, actual.CaliperHalfWidth);
            Assert.Equal(12.5, actual.Threshold); Assert.Equal(1.2, actual.Sigma);
            Assert.Equal(parameters.EdgeSelection, actual.EdgeSelection); Assert.Equal(parameters.Transition, actual.Transition);
            Assert.Null(restored.Model.CurrentCard!.LastResult);
            const string legacy = """{"Version":1,"Tool":"OpenCV/FindLine","Parameters":{"LineStart":{"X":11,"Y":25},"LineEnd":{"X":151,"Y":25},"BandHalfWidth":7.25,"CannyLow":35,"CannyHigh":110},"Rois":[]}""";
            restored.ImportProjectJson(legacy);
            actual = Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(restored.Model.CurrentParameters);
            Assert.Equal(Vision.Core.Geometry.LineSearchKind.EdgeBand, actual.SearchMode);
            Assert.Equal(new Vision.Core.Geometry.Point2D(11, 25), actual.LineStart);
            Assert.Equal(7.25, actual.BandHalfWidth); Assert.Equal(35, actual.CannyLow); Assert.Equal(110, actual.CannyHigh);
            Assert.Null(restored.Model.CurrentCard!.LastResult);
            using var reopenedLegacy = new VisionBrowserWorkspace(Catalog());
            reopenedLegacy.ImportProjectJson(restored.ExportProjectJson());
            Assert.Equal(Vision.Core.Geometry.LineSearchKind.EdgeBand,
                Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(reopenedLegacy.Model.CurrentParameters).SearchMode);
        });
    }
    [Fact]
    public void ImportProjectJson_RejectsUnknownToolWithoutChangingSelection()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(Catalog());
            var before = workspace.Model.SelectedEntry!.Name;
            var json = """{"Version":1,"Tool":"OpenCV/Missing","Rois":[]}""";
            Assert.Throws<InvalidDataException>(() => workspace.ImportProjectJson(json));
            Assert.Equal(before, workspace.Model.SelectedEntry!.Name);
        });
    }

    private static VisionBrowserCatalog Catalog() => new(
        OpenCvToolCatalog.Entries, [],
        _ => throw new InvalidOperationException("不应加载 Provider。"),
        TestBrowserViewModel.CreateInput,
        LoadImageByPath: path => Path.IsPathRooted(path)
            ? VisionImage.FromBuffer(80, 60, PixelFormat.Mono8, new byte[80 * 60], source: "restored")
            : throw new FileNotFoundException("未知图像路径。", path));
}
