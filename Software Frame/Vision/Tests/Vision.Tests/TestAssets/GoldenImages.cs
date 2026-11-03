using Vision.Core.Imaging;

namespace Vision.Tests.TestAssets;

/// <summary>
/// 独立合成图及解析真值，不录制算法输出。全为紧凑Mono8，每次创建独立帧/空间。
/// 不依赖已退役的SyntheticImages/VisionImage，黄金像素及原期望保持不变。
/// </summary>
public static class GoldenImages
{
    /// <summary>100×100，列[20,40)、行[30,60)为255：面积600、质心(29.5,44.5)。</summary>
    public static VisionFrame Rect600() => Rectangle(100, 100, 20, 30, 40, 60);
    public static (int Area, double CenterX, double CenterY) Rect600Expected() => (600, 29.5, 44.5);

    /// <summary>200×200，行99..101、列20..180为255；带中心y=100，下侧差分边缘y=101.5。</summary>
    public static VisionFrame HLine200() => Rectangle(200, 200, 20, 99, 181, 102);
    public static (double Y, double X0, double X1) HLine200Expected() => (100, 20, 180);

    /// <summary>200×200，整数像素中心到(100,120)的距离≤30为255；不是亚像素圆轮廓真值。</summary>
    public static VisionFrame Disc30()
    {
        var pixels = new byte[200 * 200];
        for (var y = 0; y < 200; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                var dx = x - 100;
                var dy = y - 120;
                if (dx * dx + dy * dy <= 30 * 30)
                {
                    pixels[y * 200 + x] = 255;
                }
            }
        }

        return Frame(200, 200, pixels);
    }

    public static (double CenterX, double CenterY, double Radius) Disc30Expected() => (100, 120, 30);

    /// <summary>16×16，列/行[3,13)为255，其余0；有方差的灰度相关模板。</summary>
    public static VisionFrame PatternTemplate() => Rectangle(16, 16, 3, 3, 13, 13);

    /// <summary>100×100黑底，把完整16×16模板原样粘到左上(50,60)，不执行匹配反算位置。</summary>
    public static VisionFrame PastePattern()
    {
        var template = PatternTemplate();
        var pixels = new byte[100 * 100];
        for (var y = 0; y < template.Height; y++)
        {
            template.Pixels.Slice(y * template.Stride, template.Width)
                .CopyTo(pixels.AsSpan((60 + y) * 100 + 50, template.Width));
        }

        return Frame(100, 100, pixels);
    }

    public static (int X, int Y) PastePatternExpected() => (50, 60);

    private static VisionFrame Rectangle(int width, int height, int x0, int y0, int x1, int y1)
    {
        var pixels = new byte[width * height];
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                pixels[y * width + x] = 255;
            }
        }

        return Frame(width, height, pixels);
    }

    private static VisionFrame Frame(int width, int height, byte[] pixels) =>
        new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
}
