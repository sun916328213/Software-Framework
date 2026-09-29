// ─────────────────────────────────────────────────────────────────────────────
// P10 —— Golden 回归集（计划 §P10「建立 Golden Image / Golden Result 回归集」）。
//
// 黄金 = 合成图 + 解析期望（构造即真值，非录制输出）：
//   · Rect600：100×100，[20,40)×[30,60) 白块 ⇒ 面积 600、质心 (29.5,44.5)；
//   · HLine200：200×200，y=100 三像素粗水平线（20..180）；
//   · Disc30：200×200，实心圆盘 r=30 @ (100,120)；
//   · PastePattern：100×100 黑底 + 16×16 模板（[3,13) 白方块）粘于 (50,60)。
// 回归测试断言黄金恒成立；算法改动导致黄金变化时必须同步更新本文件注释（显式评审）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Imaging;

namespace Vision.Tests.TestAssets;

/// <summary>Golden 图像与解析期望（回归锁）。</summary>
public static class GoldenImages
{
    /// <summary>Rect600 图像。</summary>
    public static VisionImage Rect600() => Tools.SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);

    /// <summary>Rect600 期望：面积 600，质心 (29.5, 44.5)。</summary>
    public static (int Area, double CenterX, double CenterY) Rect600Expected() => (600, 29.5, 44.5);

    /// <summary>HLine200 图像。</summary>
    public static VisionImage HLine200() => Tools.SyntheticImages.HorizontalLine(200, 200, 100, 20, 180);

    /// <summary>HLine200 期望：y=100 直线，x 覆盖 20..180。</summary>
    public static (double Y, double X0, double X1) HLine200Expected() => (100, 20, 180);

    /// <summary>Disc30 图像。</summary>
    public static VisionImage Disc30() => Tools.SyntheticImages.FilledDisc(200, 100, 120, 30);

    /// <summary>Disc30 期望：圆心 (100,120)，半径 30。</summary>
    public static (double CenterX, double CenterY, double Radius) Disc30Expected() => (100, 120, 30);

    /// <summary>PastePattern 搜索图（模板粘于 (50,60)）。</summary>
    public static VisionImage PastePattern() =>
        Tools.SyntheticImages.PasteTemplate(100, 100, Tools.SyntheticImages.PatternTemplate(), 50, 60);

    /// <summary>PastePattern 期望：左上 (50,60)，归一化相关峰 ≈1。</summary>
    public static (int X, int Y) PastePatternExpected() => (50, 60);
}
