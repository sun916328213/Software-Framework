// ─────────────────────────────────────────────────────────────────────────────
// P7 —— OpenCV Tool 结果坐标转换（与 HALCON 侧 ResultCoordinates 同语义；
// OpenCV Blob 另带 Bounds，一并按链仿射映射，见 AffineTransform2D.MapBox）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Coordinate;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Measurement;

namespace Vision.Tools.OpenCv.Coordinate;

/// <summary>OpenCV Tool 结果的坐标系转换（像素→world / 像素→机台）。</summary>
public static class ResultCoordinates
{
    /// <summary>Blob 中心 + 外接框转 world 系。</summary>
    public static BlobResult ToWorld(BlobResult pixel, CoordinateChain chain) =>
        MapBlob(pixel, RequireImageChain(chain));

    /// <summary>Blob 中心 + 外接框转机台系。</summary>
    public static BlobResult ToMachine(BlobResult pixel, CoordinateChain chain)
    {
        var verified = RequireImageChain(chain);
        return MapBlob(pixel, verified, toMachine: true);
    }

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

    private static BlobResult MapBlob(BlobResult pixel, CoordinateChain chain, bool toMachine = false)
    {
        ArgumentNullException.ThrowIfNull(pixel);
        // Bounds 与中心走同一目标系：world 用前两段仿射，机台用整链仿射。
        var affine = toMachine ? chain.ToAffine() : chain.ToAffine(2);
        var output = new BlobResult
        {
            Success = pixel.Success,
            Message = pixel.Message,
            ElapsedMs = pixel.ElapsedMs,
            Validation = pixel.Validation,
        };
        foreach (var item in pixel.Items)
        {
            var center = toMachine ? chain.PixelToMachine(item.Center) : chain.PixelToWorld(item.Center);
            output.Items.Add(new BlobItem
            {
                Index = item.Index,
                Area = item.Area,
                Center = center,
                Bounds = affine.MapBox(item.Bounds),
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
