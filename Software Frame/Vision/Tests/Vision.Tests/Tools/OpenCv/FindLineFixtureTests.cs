using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Tools.OpenCv.Measurement;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;
[Trait("Category", "Regression")]
public sealed class FindLineFixtureTests
{
    [Theory]
    [InlineData(LineSearchKind.Calipers, 0)]
    [InlineData(LineSearchKind.EdgeBand, 0)]
    [InlineData(LineSearchKind.Calipers, 90)]
    [InlineData(LineSearchKind.EdgeBand, 90)]
    public void RealFindLine_FollowsTranslationAndRotation_WhileRecipeStaysAtBaseline(LineSearchKind mode, double angle)
    {
        using var tool = Tool(mode); var capability = Assert.IsAssignableFrom<ILineSearchFixtureTool>(tool);
        var frame = Frame(angle); var run = capability.PrepareLineSearchRun(frame, new(90, 50, 0), new(100, 90, angle));
        using var result = Assert.IsType<FindLineResult>(run()); Assert.True(result.Success, result.Message);
        if (angle == 0) { Assert.InRange(result.Start.Y, 89, 90); Assert.InRange(result.End.Y, 89, 90); Assert.InRange(result.End.X - result.Start.X, 99, 101); }
        else { Assert.InRange(result.Start.X, 99, 100); Assert.InRange(result.End.X, 99, 100); Assert.InRange(result.End.Y - result.Start.Y, 99, 101); }
        Assert.Equal(new Point2D(40, 50), tool.Parameters.LineStart); Assert.Equal(new Point2D(140, 50), tool.Parameters.LineEnd);
        using var unchanged = tool.Run(frame); Assert.False(unchanged.Success); // 不移动原路径时确实找不到相距40像素的边，不以数学结果冒充算法。
        using var again = Assert.IsType<FindLineResult>(capability.PrepareLineSearchRun(frame, new(90, 50, 0), new(100, 90, angle))());
        Assert.True(again.Success); Assert.Equal(result.Start, again.Start); Assert.Equal(result.End, again.End); // 不累积漂移。
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("current")]
    [InlineData("overflow")]
    public void InvalidRigidPoseOrUnrepresentablePath_IsRejectedBeforeAlgorithm(string invalid)
    {
        using var tool = Tool(LineSearchKind.Calipers); var capability = Assert.IsAssignableFrom<ILineSearchFixtureTool>(tool);
        var reference = invalid == "reference" ? new Pose2D(double.NaN, 0, 0) : Pose2D.Identity;
        var current = invalid == "current" ? new Pose2D(0, 0, double.PositiveInfinity) : new Pose2D(100, 90, 0);
        if (invalid == "overflow") { reference = new(-double.MaxValue, 0, 0); current = new(double.MaxValue, 0, 0); }
        Assert.Throws<ArgumentException>(() => capability.PrepareLineSearchRun(Frame(0), reference, current));
    }

    [Fact]
    public void PreparedLineAndPixels_SurviveChangesAndRelease_AndNullPoseIsLegacyBaseline()
    {
        var tool = Tool(LineSearchKind.Calipers); var capability = Assert.IsAssignableFrom<ILineSearchFixtureTool>(tool); var frame = Frame(0);
        var run = capability.PrepareLineSearchRun(frame, new(90, 50, 0), new(100, 90, 0));
        tool.Parameters.LineStart = new(0, 0); tool.Parameters.Threshold = 255; frame.Pixels.AsSpan().Clear(); tool.Dispose();
        using var result = Assert.IsType<FindLineResult>(run()); Assert.True(result.Success, result.Message); Assert.InRange(result.Start.Y, 89, 90);
        using var baseline = Tool(LineSearchKind.Calipers); var pixels = new byte[200 * 180]; for (var y = 50; y < 180; y++) for (var x = 0; x < 200; x++) pixels[y * 200 + x] = 255;
        var image = VisionImage.FromBuffer(200, 180, PixelFormat.Mono8, pixels);
        using var old = baseline.Run(image); using var prepared = Assert.IsType<FindLineResult>(Assert.IsAssignableFrom<ILineSearchFixtureTool>(baseline).PrepareLineSearchRun(image, new(double.NaN, 0, 0), null)());
        Assert.True(old.Success); Assert.True(prepared.Success); Assert.Equal(old.Start, prepared.Start); Assert.Equal(old.End, prepared.End);
    }

    [Fact]
    public void NonzeroReferenceRotation_IsInvertedBeforeApplyingCurrentPose()
    {
        using var tool = Tool(LineSearchKind.Calipers); var p = tool.Parameters; p.LineStart = new(100, 40); p.LineEnd = new(100, 140);
        using var result = Assert.IsType<FindLineResult>(Assert.IsAssignableFrom<ILineSearchFixtureTool>(tool).PrepareLineSearchRun(Frame(0), new(100, 90, 90), new(100, 90, 0))());
        Assert.True(result.Success, result.Message); Assert.InRange(result.Start.Y, 89, 90); Assert.InRange(result.End.Y, 89, 90);
        Assert.Equal(new Point2D(100, 40), p.LineStart); Assert.Equal(new Point2D(100, 140), p.LineEnd);
    }
    private static FindLineTool Tool(LineSearchKind mode) => new() { Parameters = new()
    { SearchMode = mode, LineStart = new(40, 50), LineEnd = new(140, 50), BandHalfWidth = 4, CaliperHalfLength = 8, CaliperHalfWidth = 2, CaliperCount = 9,
      Threshold = 20, Sigma = 1, MinEdgePoints = 30, CannyLow = 30, CannyHigh = 80 } };
    private static VisionImage Frame(double angle)
    {
        var pixels = new byte[200 * 180]; for (var y = 0; y < 180; y++) for (var x = 0; x < 200; x++) if (angle == 0 ? y >= 90 : x >= 100) pixels[y * 200 + x] = 255;
        return VisionImage.FromBuffer(200, 180, PixelFormat.Mono8, pixels);
    }
}
