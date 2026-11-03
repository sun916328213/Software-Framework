using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.OpenCv.Inspection;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class BlobPointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealBlob_UniqueFinalCandidateEmitsActualCenter_EvenWhenItsIndexIsNotOne(bool filtered)
    {
        var image = Frame(filtered ? 2 : 1); using var tool = Tool();
        if (filtered) tool.Parameters.MinArea = 30; // 36像素保留，第二个25像素丢弃。
        using var result = tool.Run(image); Assert.True(result.Success, result.Message);
        Assert.Single(result.Items); result.Items[0].Index = 99; // 不把显示序号/排序行当目标身份。
        var numeric = Assert.IsAssignableFrom<IPointResult>(result);
        Assert.True(numeric.TryGetPointField("SingleCenter", out var point));
        Assert.Equal(new Point2D(12.5, 22.5), point!.Position); Assert.Equal(GeometryUnit.Pixel, point.Unit);
        Assert.False(numeric.TryGetPointField("Center", out _)); Assert.False(numeric.TryGetPointField("Items[0].Center", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MissingOrAmbiguousTarget_DoesNotBecomeOriginOrAutomaticallyPickFirstOrLargest(int count)
    {
        using var tool = Tool(); using var result = tool.Run(Frame(count)); Assert.True(result.Success, result.Message);
        Assert.Equal(count, result.Items.Count);
        Assert.False(Assert.IsAssignableFrom<IPointResult>(result).TryGetPointField("SingleCenter", out var point)); Assert.Null(point);
        Assert.True(result.TryGetNumericField("Count", out var number)); Assert.Equal(count, number!.Value);
    }

    [Fact]
    public void RealOriginIsValid_AndPreparedPointOutlivesSourceMutationAndDisposal()
    {
        var pixels = new byte[64 * 40]; pixels[0] = 255;
        var image = VisionImage.FromBuffer(64, 40, PixelFormat.Mono8, pixels); using var source = Tool(); using var result = source.Run(image);
        Assert.True(Assert.IsAssignableFrom<IPointResult>(result).TryGetPointField("SingleCenter", out var point)); Assert.Equal(Point2D.Origin, point!.Position);
        var geometry = new GeometryTool(); geometry.Parameters.FirstStart = new(1000, 1000); geometry.Parameters.FirstEnd = new(3, 4);
        var run = geometry.PreparePointRun(image, new("FirstStart", point with { ImageIdentity = VisionToolConfiguration.ImageIdentity(image), CoordinateIdentity = "TaskInput" }, "TaskInput"));
        result.Items.Clear(); result.Dispose(); source.Dispose(); Array.Clear(pixels); geometry.Dispose();
        using var measured = Assert.IsType<GeometryResult>(run()); Assert.True(measured.Success, measured.Message); Assert.Equal(5, measured.Distance);
        Assert.Equal(Point2D.Origin, measured.AppliedPoint!.Position);
    }

    [Fact]
    public void FailureUnexecutedDisposedAndNonFiniteCenter_DoNotEmitPoint()
    {
        using var tool = Tool(); using var failed = tool.Run(null);
        Assert.False(Assert.IsAssignableFrom<IPointResult>(failed).TryGetPointField("SingleCenter", out _));
        using var unexecuted = new BlobResult();
        Assert.False(Assert.IsAssignableFrom<IPointResult>(unexecuted).TryGetPointField("SingleCenter", out _));
        using var actual = tool.Run(Frame(1)); var points = Assert.IsAssignableFrom<IPointResult>(actual);
        actual.Items[0].Center = new(double.NaN, 0); Assert.False(points.TryGetPointField("SingleCenter", out _));
        actual.Items[0].Center = new(0, double.PositiveInfinity); Assert.False(points.TryGetPointField("SingleCenter", out _));
        actual.Items[0].Center = Point2D.Origin; actual.Dispose(); Assert.False(points.TryGetPointField("SingleCenter", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenCvMappedWorldOrMachineResults_CannotMasqueradeAsPixel_EvenWithImageReattached(bool machine)
    {
        using var tool = Tool(); using var pixel = tool.Run(Frame(1));
        var chain = new CoordinateChain(Pose2D.Identity, new AffineTransform2D(2, 0, 0, 0, 2, 0), Pose2D.Identity);
        using var mapped = machine ? Vision.Tools.OpenCv.Coordinate.ResultCoordinates.ToMachine(pixel, chain)
            : Vision.Tools.OpenCv.Coordinate.ResultCoordinates.ToWorld(pixel, chain);
        mapped.RoiImage = pixel.RoiImage;
        Assert.False(Assert.IsAssignableFrom<IPointResult>(mapped).TryGetPointField("SingleCenter", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HalconManagedContract_UsesOnlyUniquePixelCandidate_AndMappedCoordinatesRemainNonPixel(bool machine)
    {
        using var result = new Vision.Tools.Halcon.Inspection.BlobResult { RoiImage = Frame(1), Items = [new() { Center = new(3, 4) }] };
        var points = Assert.IsAssignableFrom<IPointResult>(result); Assert.True(points.TryGetPointField("SingleCenter", out var point)); Assert.Equal(new Point2D(3, 4), point!.Position);
        var chain = new CoordinateChain(Pose2D.Identity, AffineTransform2D.Identity, Pose2D.Identity);
        using var mapped = machine ? Vision.Tools.Halcon.Coordinate.ResultCoordinates.ToMachine(result, chain)
            : Vision.Tools.Halcon.Coordinate.ResultCoordinates.ToWorld(result, chain);
        mapped.RoiImage = result.RoiImage; Assert.False(Assert.IsAssignableFrom<IPointResult>(mapped).TryGetPointField("SingleCenter", out _));
        result.Items.Add(new() { Center = new(8, 9) }); Assert.False(points.TryGetPointField("SingleCenter", out _));
        result.Items.Clear(); Assert.False(points.TryGetPointField("SingleCenter", out _));
    }

    private static BlobTool Tool() => new() { Parameters = new() { MinGray = 128, MinArea = 1 } };
    private static VisionImage Frame(int count)
    {
        var pixels = new byte[64 * 40];
        if (count >= 1) for (var y = 20; y < 26; y++) for (var x = 10; x < 16; x++) pixels[y * 64 + x] = 255;
        if (count >= 2) for (var y = 5; y < 10; y++) for (var x = 40; x < 45; x++) pixels[y * 64 + x] = 255;
        return VisionImage.FromBuffer(64, 40, PixelFormat.Mono8, pixels);
    }
}
