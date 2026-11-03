using Vision.Controls.ViewModels;
using Vision.Controls.Models;
using Vision.Core.Imaging;

using Cv = Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.WorkbenchSmoke;

[Trait("Category", "Unit")]
public sealed class ToolCatalogTests
{
    [Fact]
    public void RawAndControlsCatalogs_UseSameRegistrations_KeepNamesAndCreateFreshTools()
    {
        Check(HalconToolCatalog.Tools, HalconToolCatalog.Entries, "HALCON",
            ["Blob（Inspection)", "ShapeMatch（Matching)", "FindLine（Measurement)", "Barcode（Identification)",
             "DataCode（Identification)", "NPointsCalibration（Calibration)", "Fixture（Coordinate)", "ImageFile（Acquisition)"]);
        Check(OpenCvToolCatalog.Tools, OpenCvToolCatalog.Entries, "OpenCV",
            ["Threshold（Processing)", "Morphology（Processing)", "Blob（Inspection)", "TemplateMatch（Matching)",
             "FindLine（Measurement)", "FindCircle（Measurement)", "Crop（Processing)",
             "ImageConvert（Processing)", "ImageFilter（Processing)", "GrayStatistics（Inspection)",
             "IntensityTransform（Processing)", "AdaptiveThreshold（Processing)", "ColorSegment（Processing)", "ImageFile（Acquisition)"]);
        Check(AIToolCatalog.Tools, AIToolCatalog.Entries, "AI",
            ["Classification", "Detection", "Segmentation", "AnomalyDetection"]);
    }

    [Fact]
    public void BrowserConfiguration_PreservesDefaultBlobAndTemplateCallback_WithoutChangingRawDefaults()
    {
        using var raw = Assert.IsType<Cv.BlobTool>(OpenCvToolCatalog.Create("Blob（Inspection)"));
        Assert.Equal(0, raw.Parameters.MinGray);
        var entries = HalconToolCatalog.Entries.Concat(OpenCvToolCatalog.Entries).Concat(AIToolCatalog.Entries).Concat(CommonToolCatalog.Entries).ToArray();
        Assert.Equal(28, entries.Length);
        Assert.Equal(new[] { "Common/Geometry", "Common/Tolerance" }, CommonToolCatalog.Entries.Select(e => e.Name));
        Assert.All(CommonToolCatalog.Entries, e => Assert.Equal("Common", e.Backend));
        var defaultEntry = Assert.Single(entries.Where(e => e.IsDefault));
        Assert.Equal("OpenCV/Blob", defaultEntry.Name);
        var (card, parameters, _) = defaultEntry.Create();
        using (card) Assert.Equal(128, Assert.IsType<Cv.BlobParameters>(parameters).MinGray);

        var templateEntry = Assert.Single(entries.Where(e => e.SetTemplate != null));
        Assert.Equal("OpenCV/TemplateMatch", templateEntry.Name);
        using var rawTemplate = new Vision.Tools.OpenCv.Matching.TemplateMatchTool();
        Assert.Equal(0, rawTemplate.Parameters.AngleExtentDeg); // 旧文件缺字段沿原DTO默认值，不隐式升级。
        var templateTool = templateEntry.Create();
        var poseParameters = Assert.IsType<Vision.Tools.OpenCv.Matching.TemplateMatchParameters>(templateTool.Parameters);
        Assert.Equal(-180, poseParameters.AngleStartDeg);
        Assert.Equal(360, poseParameters.AngleExtentDeg);
        using (templateTool.Card)
        {
            var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [255]);
            templateEntry.SetTemplate!(templateTool.Parameters, image);
            Assert.Same(image, Assert.IsType<Vision.Tools.OpenCv.Matching.TemplateMatchParameters>(templateTool.Parameters).Template);
        }
    }

    [Fact]
    public void NodeFields_HaveOneDefinitionPerTool_AndReferenceOriginalParameterProperties()
    {
        var entries = HalconToolCatalog.Entries.Concat(OpenCvToolCatalog.Entries).Concat(AIToolCatalog.Entries).Concat(CommonToolCatalog.Entries).ToArray();
        Assert.Equal(28, entries.Length);
        foreach (var entry in entries)
        {
            var definition = Vision.FlowNode.VisionNodeDefinitions.Get(entry.Name);
            Assert.Same(definition.Parameters, entry.NodeParameterNames);
            Assert.Same(definition.NumericOutputs, entry.NumericResultFields);
            Assert.Same(definition.LineOutputs, entry.LineResultFields);
            Assert.Same(definition.CircleOutputs, entry.CircleResultFields);
            Assert.Same(definition.PointOutputs, entry.PointResultFields);
            Assert.Same(definition.BooleanOutputs, entry.BooleanResultFields);
            var created = entry.Create();
            using (created.Card)
                foreach (var name in definition.Parameters)
                    Assert.NotNull(created.Parameters.GetType().GetProperty(name));
        }
        Assert.Equal(new[] { "RadiusMin", "RadiusMax" }, Vision.FlowNode.VisionNodeDefinitions.Get("OpenCV/FindCircle").Parameters);
        Assert.Equal(new[] { "Circle" }, Vision.FlowNode.VisionNodeDefinitions.Get("OpenCV/FindCircle").CircleOutputs);
        Assert.Equal(new[] { "Center" }, Vision.FlowNode.VisionNodeDefinitions.Get("OpenCV/FindCircle").PointOutputs);
        Assert.Equal(new[] { "Mode", "MinGray", "MaxGray" }, Vision.FlowNode.VisionNodeDefinitions.Get("OpenCV/Threshold").Parameters);
        Assert.DoesNotContain(typeof(App).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == "Vision.Workbench");
    }

    private static void Check(IReadOnlyDictionary<string, Func<object>> rawFactories, IReadOnlyList<ToolEntry> entries,
        string backend, string[] expectedKeys)
    {
        Assert.Equal(expectedKeys, rawFactories.Keys.ToArray());
        Assert.Equal(expectedKeys.Length, entries.Count);
        for (var i = 0; i < expectedKeys.Length; i++)
        {
            using var raw = Assert.IsAssignableFrom<IDisposable>(rawFactories[expectedKeys[i]]());
            var entry = entries[i];
            var created = entry.Create();
            var next = entry.Create();
            using (created.Card)
            using (next.Card)
            {
                var runtime = Assert.IsAssignableFrom<Vision.Core.Tools.IImageTool>(raw);
                var cardRuntime = Assert.IsAssignableFrom<Vision.Core.Tools.IImageTool>(created.Card);
                Assert.Equal(created.Card.Name, cardRuntime.Name);
                Assert.Equal(created.Card.Validate().IsValid, cardRuntime.Validate().IsValid);
                Assert.Equal(created.Card.LastElapsedMs, cardRuntime.LastElapsedMs);
                Assert.Equal(runtime.Name, cardRuntime.Name);
                var rawParameters = raw.GetType().GetProperty("Parameters")!.GetValue(raw);
                var shortName = expectedKeys[i].Split('（')[0];
                Assert.Equal($"{backend}/{shortName}", entry.Name);
                Assert.Equal(backend, entry.Backend);
                Assert.Equal(rawParameters!.GetType(), created.Parameters.GetType());
                Assert.NotSame(rawParameters, created.Parameters);
                Assert.NotSame(created.Parameters, next.Parameters);
                Assert.NotSame(created.Card, next.Card);
                Assert.Equal(backend == "AI", created.SetProvider != null);
                Assert.Equal(backend == "AI", entry.SupportsModelLoading);
            }
        }
    }
}
