// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SyntheticImages。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P4 测试共享 —— 合成图（解析解已知，无需相机 / Runtime）。
// 所有断言的期望值都由构造参数解析算出，不是“跑一遍把输出抄下来”的金丝雀。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Imaging;

namespace Vision.Tests.Tools;

/// <summary>
/// 合成测试图工厂（Mono8 紧凑排布）。
/// </summary>
public static class SyntheticImages
{
    /// <summary>
    /// 黑底白矩形。填充列 [x0,x1)、行 [y0,y1)。
    /// </summary>
    public static VisionImage WhiteRectangle(int width, int height, int x0, int y0, int x1, int y1)
    {
        var pixels = new byte[width * height];
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
                pixels[y * width + x] = 255;
        }

        return VisionImage.FromBuffer(width, height, PixelFormat.Mono8, pixels, source: "synthetic-rect");
    }

    /// <summary>
    /// 黑底水平白线。行 [y-1,y+1]、列 [x0,x1] 填白（3 像素粗）。
    /// </summary>
    public static VisionImage HorizontalLine(int width, int height, int y, int x0, int x1)
    {
        var pixels = new byte[width * height];
        for (var r = y - 1; r <= y + 1; r++)
        {
            for (var x = x0; x <= x1; x++)
                pixels[r * width + x] = 255;
        }

        return VisionImage.FromBuffer(width, height, PixelFormat.Mono8, pixels, source: "synthetic-line");
    }

    /// <summary>
    /// 黑底白色实心圆盘（圆心 cx,cy，半径 r，含边界）。
    /// </summary>
    public static VisionImage FilledDisc(int size, int cx, int cy, int r)
    {
        var pixels = new byte[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r * r)
                    pixels[y * size + x] = 255;
            }
        }

        return VisionImage.FromBuffer(size, size, PixelFormat.Mono8, pixels, source: "synthetic-disc");
    }

    /// <summary>
    /// 模板（16×16：黑底，[3,13)×[3,13) 白方块，有方差，适配归一化相关）。
    /// </summary>
    public static VisionImage PatternTemplate() => WhiteRectangle(16, 16, 3, 3, 13, 13);

    /// <summary>
    /// 把模板原样粘贴到黑底搜索图左上角 (pasteX,pasteY) 处。
    /// </summary>
    public static VisionImage PasteTemplate(int width, int height, VisionImage template, int pasteX, int pasteY)
    {
        var pixels = new byte[width * height];
        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
                pixels[(pasteY + y) * width + pasteX + x] = template.Pixels![y * template.Width + x];
        }

        return VisionImage.FromBuffer(width, height, PixelFormat.Mono8, pixels, source: "synthetic-paste");
    }
}
