using System.IO;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class PortableProjectTests(WorkbenchAppFixture host)
{
    [Fact]
    public void RepeatedSave_ReusesCurrentPath_WithoutOpeningSaveAsAgain()
    {
        host.Invoke(() =>
        {
            var path = TemporaryPath(); var picks = 0;
            try
            {
                using var workspace = new VisionBrowserWorkspace(Catalog(save => { picks++; return path; }));
                workspace.SaveProject();
                Assert.IsType<Parameters>(workspace.Model.CurrentParameters).Value = 9;
                workspace.SaveProject();
                Assert.Equal(1, picks);
                using var reopened = new VisionBrowserWorkspace(Catalog(_ => path));
                reopened.ImportProject();
                Assert.Equal(9, Assert.IsType<Parameters>(reopened.Model.CurrentParameters).Value);
            }
            finally { File.Delete(path); }
        });
    }

    [Theory]
    [InlineData(PixelFormat.Mono8, 1)]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public void Package_RestoresTheActualMemoryImageWithoutSourceFile_IncludingStrideAndMetadata(PixelFormat format, int bpp)
    {
        host.Invoke(() =>
        {
            var path = TemporaryPath();
            var stride = 3 * bpp + 3;
            var expected = Enumerable.Range(0, stride * 2).Select(i => (byte)(i + 1)).ToArray();
            var image = VisionImage.FromBuffer(3, 2, format, expected.ToArray(), stride, Path.Combine(Path.GetTempPath(), "deleted-source.bmp"));
            image.FrameId = 71; image.Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            try
            {
                using var workspace = new VisionBrowserWorkspace(Catalog(_ => path, image));
                workspace.LoadImage(); workspace.SaveProject();
                Array.Fill(image.Pixels, (byte)0); // 落盘快照不借用原缓冲，也不重新从原文件读取。
                using var reopened = new VisionBrowserWorkspace(Catalog(_ => path));
                reopened.ImportProject();
                var restored = Assert.IsType<VisionImage>(reopened.Model.InputImage);
                Assert.Equal(expected, restored.Pixels); Assert.Equal(3, restored.Width); Assert.Equal(2, restored.Height);
                Assert.Equal(stride, restored.Stride); Assert.Equal(format, restored.Format);
                Assert.Equal(71, restored.FrameId); Assert.Equal(image.Timestamp, restored.Timestamp);
                Assert.Null(reopened.Model.CurrentCard!.LastResult);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void SaveAs_CancelOrWriteFailureKeepsTheCurrentPath_AndSuccessMovesIt()
    {
        host.Invoke(() =>
        {
            var first = TemporaryPath(); var second = TemporaryPath(); string? selected = first;
            try
            {
                using var workspace = new VisionBrowserWorkspace(Catalog(_ => selected));
                workspace.SaveProject(); Assert.Equal(first, workspace.Model.CurrentProjectPath);
                selected = null; workspace.SaveProjectAs(); Assert.Equal(first, workspace.Model.CurrentProjectPath);
                selected = Path.Combine(TemporaryPath(), "missing", "project.visionproj");
                workspace.SaveProjectAs(); Assert.Equal(first, workspace.Model.CurrentProjectPath);
                Assert.Contains("失败", workspace.Model.ImageInfoText);
                selected = second; workspace.SaveProjectAs(); Assert.Equal(second, workspace.Model.CurrentProjectPath);
                Assert.True(File.Exists(first)); Assert.True(File.Exists(second));
            }
            finally { File.Delete(first); File.Delete(second); }
        });
    }

    [Fact]
    public void LegacyJson_UsesRelativeImagePath_AndSaveUpgradesWithoutOverwritingTheOldJson()
    {
        host.Invoke(() =>
        {
            var old = Path.ChangeExtension(TemporaryPath(), ".json"); var next = TemporaryPath();
            var reads = new List<string>(); var picks = new List<bool>();
            const string json = """{"Version":1,"Tool":"Fake","Parameters":{"Value":8},"ImageSource":"relative.bmp","Rois":[]}""";
            try
            {
                File.WriteAllText(old, json);
                var catalog = Catalog(save => { picks.Add(save); return save ? next : old; }) with
                {
                    LoadImageByPath = path => { reads.Add(path); return VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [123]); }
                };
                using var workspace = new VisionBrowserWorkspace(catalog);
                workspace.ImportProject();
                Assert.Equal(Path.Combine(Path.GetDirectoryName(old)!, "relative.bmp"), Assert.Single(reads));
                Assert.True(workspace.Model.IsLegacyProject);
                workspace.SaveProject();
                Assert.Equal(json, File.ReadAllText(old));
                Assert.Equal(next, workspace.Model.CurrentProjectPath); Assert.False(workspace.Model.IsLegacyProject);
                Assert.Equal(new[] { false, true }, picks);
            }
            finally { File.Delete(old); File.Delete(next); }
        });
    }

    [Fact]
    public void CorruptedPackage_PreservesTheLiveSessionAndCurrentPath()
    {
        host.Invoke(() =>
        {
            var path = TemporaryPath(); var bad = TemporaryPath();
            string selected = path;
            try
            {
                var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 3, 4]);
                using var workspace = new VisionBrowserWorkspace(Catalog(_ => selected, image));
                workspace.LoadImage(); workspace.SaveProject();
                File.Copy(path, bad);
                using (var zip = System.IO.Compression.ZipFile.Open(bad, System.IO.Compression.ZipArchiveMode.Update))
                {
                    zip.GetEntry("images/input.bin")!.Delete();
                    using var stream = zip.CreateEntry("images/input.bin").Open();
                    stream.Write([9, 9, 9, 9]);
                }
                var card = workspace.Model.CurrentCard;
                selected = bad; workspace.ImportProject();
                Assert.Same(card, workspace.Model.CurrentCard); Assert.Same(image, workspace.Model.InputImage);
                Assert.Equal(path, workspace.Model.CurrentProjectPath); Assert.Contains("校验失败", workspace.Model.ImageInfoText);
            }
            finally { File.Delete(path); File.Delete(bad); }
        });
    }

    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"portable-vision-{Guid.NewGuid():N}.visionproj");
    private static VisionBrowserCatalog Catalog(Func<bool, string?> pick, VisionImage? image = null) => new(
        [new("Fake", "Test", () => (new FakeTool(), new Parameters(), null))], [],
        _ => throw new InvalidOperationException(), () => image,
        PickProjectPath: pick, LoadImageByPath: _ => throw new InvalidOperationException("must not read external image"));
    public sealed class Parameters { public int Value { get; set; } = 2; }
    private sealed class FakeTool : IExecutableTool
    {
        public string Name => "Fake";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => throw new InvalidOperationException("must not execute");
        public void EnsureLoaded() => throw new InvalidOperationException("must not load models");
        public void Dispose() { }
    }
}
