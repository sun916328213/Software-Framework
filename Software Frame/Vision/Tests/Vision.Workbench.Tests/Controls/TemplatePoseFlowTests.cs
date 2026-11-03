using System.IO;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Matching;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class TemplatePoseFlowTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(1d)]
    [InlineData(2d)]
    public async Task NodePropertiesAndFlowReopen_PreserveActualAngleScaleAndOriginalSession(double scale)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Path.Combine(Path.GetTempPath(), $"template-pose-{Guid.NewGuid():N}.flow");
        VisionToolNodeBase locator = null!;
        try
        {
            host.Invoke(() =>
            {
                locator = vm.Add(vm.ToolEntries.Single(entry => entry.Name == "OpenCV/TemplateMatch"));
                var workspace = (VisionToolInstanceWorkspace)locator.Session;
                var p = Assert.IsType<TemplateMatchParameters>(workspace.Parameters);
                foreach (var name in new[] { "AngleStartDeg", "AngleExtentDeg", "AngleStepDeg", "ScaleMin", "ScaleMax", "ScaleStep", "MaxOverlap" })
                    Assert.Single(vm.PropertyGridVM.Groups.SelectMany(group => group.Items), row => row.PropertyName == name);
                var angleRow = vm.PropertyGridVM.Groups.SelectMany(group => group.Items).Single(row => row.PropertyName == "AngleStartDeg");
                vm.Chart.Model.IsModified = false; angleRow.Value = 80d;
                Assert.Equal(80, p.AngleStartDeg); Assert.True(vm.Chart.Model.IsModified);
                Assert.Same(workspace.Editor.CurrentParameters, p);
                p.AngleExtentDeg = 20; p.AngleStepDeg = 5; p.ScaleMin = scale; p.ScaleMax = scale; p.MinScore = 0.94;
                var (template, scene) = Images(scale);
                workspace.Editor.TemplateEditor!.Capture(template); vm.SetImage(scene);
                Assert.True(vm.Chart.SaveDocument(path));
                Assert.True(reopened.Chart.LoadDocument(path));
                var restored = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model);
                Assert.Equal(locator.Id, restored.Id);
                var restoredParameters = Assert.IsType<TemplateMatchParameters>(restored.Session.Parameters);
                Assert.Equal(80, restoredParameters.AngleStartDeg); Assert.Equal(20, restoredParameters.AngleExtentDeg);
                Assert.Equal(scale, restoredParameters.ScaleMin); Assert.Equal(scale, restoredParameters.ScaleMax);
                Assert.NotSame(p, restoredParameters);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var restored = (VisionToolNodeBase)reopened.Chart.Nodes[0].Model;
                var result = Assert.IsType<TemplateMatchResult>(restored.Result);
                var match = Assert.Single(result.Matches);
                Assert.Equal(90, match.AngleDeg, 6); Assert.Equal(scale, match.Scale, 6);
                Assert.Equal(4, match.Corners.Count);
                var row = Assert.Single(VisionDisplay.ResultItems(result));
                Assert.Equal(90, row.AngleDeg); Assert.Equal(scale, row.Scale);
                Assert.Single(row.Contours); Assert.True(row.HitTest(match.Center, 0));
                using var table = VisionResultTable.Create(result)!;
                Assert.Equal(90d, table.View[0]["角度（度）"]); Assert.Equal(scale, table.View[0]["缩放"]);
                if (scale != 1)
                {
                    var consumer = reopened.Add(reopened.ToolEntries.Single(entry => entry.Name == "OpenCV/Blob"));
                    VisionFixtureBinding.Select(consumer, restored);
                    var failure = Assert.Throws<InvalidOperationException>(() => VisionFixtureBinding.Resolve(consumer));
                    Assert.Contains("非单位", failure.Message);
                }
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            await host.Invoke(() => reopened.DisposeAsync().AsTask());
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static (VisionImage Template, VisionImage Scene) Images(double scale)
    {
        const int w = 16, h = 12, width = 96, height = 80;
        var template = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) template[y * w + x] = (byte)(30 + (x * 31 + y * 17 + x * y * 7) % 190);
        var pixels = Enumerable.Repeat((byte)47, width * height).ToArray();
        var cx = 24 + h * scale / 2; var cy = 20 + w * scale / 2;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                // 独立90度逆映射，不调用被测工具或OpenCV WarpAffine生成真值。
                var u = (y + 0.5 - cy) / scale + (w - 1) / 2d;
                var v = -(x + 0.5 - cx) / scale + (h - 1) / 2d;
                if (u < 0 || v < 0 || u > w - 1 || v > h - 1) continue;
                var left = (int)u; var top = (int)v; var right = Math.Min(left + 1, w - 1); var bottom = Math.Min(top + 1, h - 1);
                var a = u - left; var b = v - top;
                pixels[y * width + x] = (byte)Math.Round((1 - a) * (1 - b) * template[top * w + left] + a * (1 - b) * template[top * w + right]
                    + (1 - a) * b * template[bottom * w + left] + a * b * template[bottom * w + right]);
            }
        return (VisionImage.FromBuffer(w, h, PixelFormat.Mono8, template), VisionImage.FromBuffer(width, height, PixelFormat.Mono8, pixels));
    }
}
