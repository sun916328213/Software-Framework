// ─────────────────────────────────────────────────────────────────────────────
// P7 —— Tool 结果坐标转换（计划 §P7「Tool Result 可转换到统一坐标系」）。
//
// 本转换只动几何（中心/起终点经链映射），不重复跑算法；链由调用方装配
// （Fixture 姿态 + 标定仿射 + 机台位姿，见 CoordinateChain）。
// 链源必须为 Image 系，否则抛（误用 world 链重复映射是典型事故，拦住）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Coordinate;
using Vision.Tools.Halcon.Inspection;
using Vision.Tools.Halcon.Measurement;

namespace Vision.Tools.Halcon.Coordinate;

/// <summary>HALCON Tool 结果的坐标系转换（像素→world / 像素→机台）。</summary>
public static class ResultCoordinates
{
    /// <summary>Blob 中心转 world 系（面积/序号原样；框尺寸按链仿射列模缩放）。</summary>
    public static BlobResult ToWorld(BlobResult pixel, CoordinateChain chain) =>
        MapBlob(pixel, RequireImageChain(chain).PixelToWorld);

    /// <summary>Blob 中心转机台系。</summary>
    public static BlobResult ToMachine(BlobResult pixel, CoordinateChain chain) =>
        MapBlob(pixel, RequireImageChain(chain).PixelToMachine);

    /// <summary>找线起终点转 world 系。</summary>
    public static FindLineResult ToWorld(FindLineResult pixel, CoordinateChain chain) =>
        MapFindLine(pixel, RequireImageChain(chain).PixelToWorld);

    /// <summary>找线起终点转机台系。</summary>
    public static FindLineResult ToMachine(FindLineResult pixel, CoordinateChain chain) =>
        MapFindLine(pixel, RequireImageChain(chain).PixelToMachine);

    private static CoordinateChain RequireImageChain(CoordinateChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (chain.Source != CoordinateSpace.Image)
            throw new ArgumentException($"坐标链源必须为 Image，实际 {chain.Source}（防止重复映射）。", nameof(chain));
        return chain;
    }

    private static BlobResult MapBlob(BlobResult pixel, Func<Vision.Core.Geometry.Point2D, Vision.Core.Geometry.Point2D> map)
    {
        ArgumentNullException.ThrowIfNull(pixel);
        var output = new BlobResult
        {
            Success = pixel.Success,
            Message = pixel.Message,
            ElapsedMs = pixel.ElapsedMs,
            Validation = pixel.Validation,
        };
        foreach (var item in pixel.Items)
        {
            output.Items.Add(new BlobItem
            {
                Index = item.Index,
                Area = item.Area,
                Center = map(item.Center),
            });
        }

        return output;
    }

    private static FindLineResult MapFindLine(FindLineResult pixel, Func<Vision.Core.Geometry.Point2D, Vision.Core.Geometry.Point2D> map)
    {
        ArgumentNullException.ThrowIfNull(pixel);
        return new FindLineResult
        {
            Success = pixel.Success,
            Message = pixel.Message,
            ElapsedMs = pixel.ElapsedMs,
            Validation = pixel.Validation,
            Start = map(pixel.Start),
            End = map(pixel.End),
            EdgePointCount = pixel.EdgePointCount,
        };
    }
}
