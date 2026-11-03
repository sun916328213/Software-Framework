using Vision.Core.Geometry;
using Vision.Core.Tools;
using Xunit;
using Xunit.Abstractions;
using HalconBlob = Vision.Tools.Halcon.Inspection.BlobTool;
using HalconResult = Vision.Tools.Halcon.Inspection.BlobResult;
using OpenCvBlob = Vision.Tools.OpenCv.Inspection.BlobTool;
using OpenCvResult = Vision.Tools.OpenCv.Inspection.BlobResult;

namespace Vision.Tests.Tools.Comparison;

// Explicit opt-in preserves the normal CI runtime boundary while allowing a real, repeatable smoke test.
public sealed class DualBackendRuntimeFactAttribute : FactAttribute
{
    public DualBackendRuntimeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VISION_DUAL_BACKEND_SMOKE") != "1")
            Skip = "Set VISION_DUAL_BACKEND_SMOKE=1 in an environment with HALCON Runtime and a valid license.";
    }
}

[Trait("Category", "RequiresRuntime")]
public sealed class DualBackendRuntimeSmokeTests(ITestOutputHelper output)
{
    [DualBackendRuntimeFact]
    public void Blob_ProcessesSameImageThroughCommonInterface()
    {
        var image = SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);
        using var halcon = new HalconBlob();
        using var openCv = new OpenCvBlob();
        halcon.Parameters.MinGray = openCv.Parameters.MinGray = 128;
        halcon.Parameters.Features.Clear();
        openCv.Parameters.Features.Clear();

        using var hResult = ((IImageTool)halcon).Run(image);
        using var cvResult = ((IImageTool)openCv).Run(image);
        Assert.True(hResult.Success, $"HALCON: {hResult.Message}");
        Assert.True(cvResult.Success, $"OpenCV: {cvResult.Message}");
        var h = Assert.Single(Assert.IsType<HalconResult>(hResult).Items);
        var cv = Assert.Single(Assert.IsType<OpenCvResult>(cvResult).Items);
        output.WriteLine($"HALCON area={h.Area}, center=({h.Center.X},{h.Center.Y}); OpenCV area={cv.Area}, center=({cv.Center.X},{cv.Center.Y})");
        var expected = new Point2D(29.5, 44.5);
        Assert.Equal(600, h.Area);
        Assert.Equal(600, cv.Area);
        Assert.True(h.Center.DistanceTo(expected) <= 1.0);
        Assert.True(cv.Center.DistanceTo(expected) <= 1.0);
        Assert.True(h.Center.DistanceTo(cv.Center) <= 1.0);
    }
}
