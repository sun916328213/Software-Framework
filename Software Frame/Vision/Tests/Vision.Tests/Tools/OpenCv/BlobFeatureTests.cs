using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Tests.Tools;
using Vision.Tools.OpenCv.Inspection;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class BlobFeatureTests
{
    private const double Phi = Math.PI / 2;

    [Fact]
    public void CentroidAndExtentFeatures_MatchAnalyticValues_ForSeparatedComponents()
    {
        var image = ThreeComponents();

        var square = Assert.Single(Run(image, ("column", 9.49, 9.51)));
        Assert.Equal(new Rect2D(5, 5, 10, 10), square.Bounds);
        Assert.Equal(9.5, square.Center.X, 6);
        Assert.Equal(9.5, square.Center.Y, 6);

        var wide = Assert.Single(Run(image, ("width", 19.99, 20.01), ("height", 9.99, 10.01)));
        Assert.Equal(new Rect2D(25, 5, 20, 10), wide.Bounds);

        var tall = Assert.Single(Run(image, ("row", 14.49, 14.51), ("column", 54.49, 54.51)));
        Assert.Equal(new Rect2D(50, 5, 10, 20), tall.Bounds);

        Assert.Empty(Run(image, ("width", 19.99, 20.01), ("height", 19.99, 20.01)));
    }

    [Fact]
    public void Phi_ReportsMajorAxisOrientation_ForAxisAlignedComponents()
    {
        var image = ThreeComponents();

        // 横条与方块的主轴沿列方向（0）；竖条主轴向行方向（π/2）。
        var horizontal = Run(image, ("phi", -0.0001, 0.0001));
        Assert.Equal(new[] { new Rect2D(5, 5, 10, 10), new Rect2D(25, 5, 20, 10) },
            horizontal.Select(item => item.Bounds));

        var vertical = Assert.Single(Run(image, ("phi", Phi - 0.0001, Phi + 0.0001)));
        Assert.Equal(new Rect2D(50, 5, 10, 20), vertical.Bounds);

        Assert.Empty(Run(image, ("phi", Phi / 2 - 0.01, Phi / 2 + 0.01)));
    }

    [Fact]
    public void SquareFeatures_MatchAnalyticCircleRectAndRadiiValues()
    {
        var image = SyntheticImages.WhiteRectangle(50, 50, 10, 10, 20, 20);

        // 10×10 像素：面积 100，像素边界周长 40，圆度 4πA/P² = π/4。
        Assert.Single(Run(image, ("circularity", Math.PI / 4 - 1e-3, Math.PI / 4 + 1e-3)));
        Assert.Empty(Run(image, ("circularity", 0.99, 1.01)));

        Assert.Single(Run(image, ("rectangularity", 0.9999, 1.0001)));
        // 外接圆半径：半对角线 √(10²+10²)/2；内切圆半径：边长一半。
        Assert.Single(Run(image, ("outer_radius", Math.Sqrt(50) - 1e-3, Math.Sqrt(50) + 1e-3)));
        Assert.Single(Run(image, ("inner_radius", 4.999, 5.001)));
        Assert.Empty(Run(image, ("inner_radius", 4.4, 4.6)));
    }

    [Fact]
    public void SteppedShape_HasLowerRectangularity_ThanItsBoundingBox()
    {
        const int width = 40;
        var pixels = new byte[width * width];
        for (var y = 5; y < 25; y++)
            for (var x = 5; x < (y < 15 ? 15 : 15 + 10); x++) pixels[y * width + x] = 255;

        // 面积 300，最小外接矩形 20×20 → 矩形度 0.75。
        var image = VisionImage.FromBuffer(width, width, PixelFormat.Mono8, pixels);
        Assert.Single(Run(image, ("area", 299.99, 300.01), ("rectangularity", 0.749, 0.751)));
        Assert.Empty(Run(image, ("rectangularity", 0.9999, 1.0001)));
    }

    [Fact]
    public void SinglePixel_RadiiMatchHalconPixelSemantics()
    {
        const int width = 15;
        var pixels = new byte[width * width];
        pixels[7 * width + 7] = 255;
        var image = VisionImage.FromBuffer(width, width, PixelFormat.Mono8, pixels);

        Assert.Single(Run(image, ("outer_radius", Math.Sqrt(0.5) - 1e-3, Math.Sqrt(0.5) + 1e-3)));
        Assert.Single(Run(image, ("inner_radius", 0.499, 0.501)));
    }

    [Fact]
    public void PreparedRun_CopiesMinArea_SoBackgroundExecutionFilters()
    {
        var image = SyntheticImages.WhiteRectangle(20, 20, 5, 5, 4, 4); // 面积 16。
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.MinArea = 17;
        var execution = tool.PrepareRun(image);
        tool.Parameters.MinArea = 0; // 快照生成后修改原工具不得影响后台执行。
        using var result = Assert.IsType<BlobResult>(execution());
        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void MinArea_DropsSmallComponents_BeforeFeatureFiltering()
    {
        var image = ThreeComponents(); // 面积分别为 100、200、200。

        Assert.Equal(3, Run(image, 0).Count);
        Assert.Equal(new[] { new Rect2D(25, 5, 20, 10), new Rect2D(50, 5, 10, 20) },
            Run(image, 150).Select(item => item.Bounds));
        Assert.Empty(Run(image, 201));
        // 面积预筛与特征筛选共同生效，而不是互相替代。
        Assert.Equal(new Rect2D(50, 5, 10, 20), Assert.Single(Run(image, 150, ("height", 15, 25))).Bounds);
    }

    [Fact]
    public void DefaultMinArea_Ten_DropsComponentsSmallerThanTenPixels()
    {
        const int width = 20;
        var pixels = new byte[width * width];
        for (var y = 5; y < 8; y++)
            for (var x = 5; x < 8; x++) pixels[y * width + x] = 255; // 面积 9。
        var image = VisionImage.FromBuffer(width, width, PixelFormat.Mono8, pixels);
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features = [];

        using var filtered = tool.Run(image);
        Assert.True(filtered.Success, filtered.Message);
        Assert.Empty(filtered.Items);

        tool.Parameters.MinArea = 0;
        using var kept = tool.Run(image);
        Assert.Equal(9, Assert.Single(kept.Items).Area);
    }

    [Fact]
    public void ShapeFeatureFilters_HonorAndOrRelation()
    {
        var image = ThreeComponents();

        var and = Run(image, BlobFeatureLogic.And, ("width", 9.99, 10.01), ("height", 15, 25));
        Assert.Equal(new Rect2D(50, 5, 10, 20), Assert.Single(and).Bounds);

        var or = Run(image, BlobFeatureLogic.Or, ("width", 15, 25), ("height", 15, 25));
        Assert.Equal(new[] { new Rect2D(25, 5, 20, 10), new Rect2D(50, 5, 10, 20) },
            or.Select(item => item.Bounds));
    }

    private static IReadOnlyList<BlobItem> Run(
        VisionImage image, BlobFeatureLogic logic, params (string Feature, double Min, double Max)[] filters) =>
        Run(image, logic, 0, filters);

    private static IReadOnlyList<BlobItem> Run(
        VisionImage image, int minArea, params (string Feature, double Min, double Max)[] filters) =>
        Run(image, BlobFeatureLogic.And, minArea, filters);

    private static IReadOnlyList<BlobItem> Run(
        VisionImage image, BlobFeatureLogic logic, int minArea,
        params (string Feature, double Min, double Max)[] filters)
    {
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.MinArea = minArea;
        tool.Parameters.Logic = logic;
        tool.Parameters.Features = filters
            .Select(f => new BlobFeatureRange { Feature = f.Feature, Min = f.Min, Max = f.Max })
            .ToList();
        using var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        return result.Items;
    }

    private static IReadOnlyList<BlobItem> Run(
        VisionImage image, params (string Feature, double Min, double Max)[] filters) =>
        Run(image, BlobFeatureLogic.And, filters);

    private static VisionImage ThreeComponents()
    {
        const int width = 70;
        var pixels = new byte[width * 40];
        for (var y = 5; y < 15; y++)
        {
            for (var x = 5; x < 15; x++) pixels[y * width + x] = 255;
            for (var x = 25; x < 45; x++) pixels[y * width + x] = 255;
        }
        for (var y = 5; y < 25; y++)
            for (var x = 50; x < 60; x++) pixels[y * width + x] = 255;
        return VisionImage.FromBuffer(width, 40, PixelFormat.Mono8, pixels);
    }
}
