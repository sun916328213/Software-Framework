using System.Runtime.InteropServices;
using HalconDotNet;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Tools.Halcon.Engine;

internal sealed partial class HalconVisionRuntime
{
    public BinaryImageOutput Threshold(ThresholdRangeOperation operation) => Use(resources =>
    {
        var frame = operation.Image;
        var image = ProcessingImage(frame, operation.Region, resources);
        HOperatorSet.Threshold(image, out var region, operation.Minimum, operation.Maximum);
        resources.Add(region);
        // 按原尺寸导出；参与域外自然为0，不将域外像素当作参与处理。
        HOperatorSet.RegionToBin(region, out var binary, 255, 0, frame.Width, frame.Height);
        resources.Add(binary);
        return new BinaryImageOutput(frame.FrameId,
            CopyGrayOutput(binary, frame.Width, frame.Height, PixelFormat.Mono8, frame.CoordinateSpaceId, resources));
    });

    public GrayStatisticsOutput Statistics(GrayStatisticsOperation operation) => Use(resources =>
    {
        var frame = operation.Image;
        var image = ProcessingImage(frame, operation.Region, resources);
        HOperatorSet.GetDomain(image, out var domain);
        resources.Add(domain);
        HOperatorSet.AreaCenter(domain, out var area, out var row, out var column);
        resources.Add(area);
        resources.Add(row);
        resources.Add(column);
        if (area.Length != 1 || area.D <= 0)
        {
            throw new InvalidOperationException("统计域没有有效参与像素。");
        }

        HOperatorSet.MinMaxGray(domain, image, 0, out var minimum, out var maximum, out var range);
        resources.Add(minimum);
        resources.Add(maximum);
        resources.Add(range);
        HOperatorSet.Intensity(domain, image, out var mean, out var deviation);
        resources.Add(mean);
        resources.Add(deviation);
        return new GrayStatisticsOutput(frame.FrameId, frame.CoordinateSpaceId, frame.Format, checked((long)area.D),
            minimum.D, maximum.D, mean.D, deviation.D);
    });

    public ImageCropOutput Crop(ImageCropOperation operation) => Use(resources =>
    {
        var frame = operation.Image;
        var image = Image(frame, resources);
        // HALCON 下/右端包含；Core 的宽高为半开矩形长度，边界在此显式换算。
        HOperatorSet.CropRectangle1(image, out var cropped, operation.Y, operation.X,
            operation.Y + operation.Height - 1, operation.X + operation.Width - 1);
        resources.Add(cropped);
        var output = CopyGrayOutput(cropped, operation.Width, operation.Height, frame.Format, Guid.NewGuid(), resources);
        return new ImageCropOutput(frame.FrameId, frame.CoordinateSpaceId, output, operation.X, operation.Y);
    });

    private static HObject ProcessingImage(VisionFrame frame, SearchRegion? region, List<IDisposable> resources)
    {
        var image = Image(frame, resources);
        if (region == null)
        {
            return image;
        }

        // 复用 Core 的像素中心参与规则，避免 SDK 几何取整使两端处理不同像素。
        var runs = region.Runs(frame.Width, frame.Height).ToArray();
        if (runs.Length == 0)
        {
            throw new ArgumentException("处理域与图像没有有效参与像素。");
        }

        var rows = new HTuple(runs.Select(run => run.Row).ToArray());
        resources.Add(rows);
        var starts = new HTuple(runs.Select(run => run.Start).ToArray());
        resources.Add(starts);
        var ends = new HTuple(runs.Select(run => run.End).ToArray());
        resources.Add(ends);
        HOperatorSet.GenRegionRuns(out var domain, rows, starts, ends);
        resources.Add(domain);
        HOperatorSet.ReduceDomain(image, domain, out var reduced);
        resources.Add(reduced);
        return reduced;
    }

    private static VisionFrame CopyGrayOutput(
        HObject image,
        int expectedWidth,
        int expectedHeight,
        PixelFormat format,
        Guid space,
        List<IDisposable> resources)
    {
        HOperatorSet.GetImagePointer1(image, out var pointer, out var type, out var width, out var height);
        resources.Add(pointer);
        resources.Add(type);
        resources.Add(width);
        resources.Add(height);
        var expectedType = format == PixelFormat.Mono16 ? "uint2" : "byte";
        if (type.S != expectedType || width.I != expectedWidth || height.I != expectedHeight)
        {
            throw new InvalidDataException("HALCON输出位深或尺寸与本次操作不符。");
        }

        var bytes = new byte[checked(expectedWidth * expectedHeight * (format == PixelFormat.Mono16 ? 2 : 1))];
        Marshal.Copy(pointer.IP, bytes, 0, bytes.Length);
        return new VisionFrame(Guid.NewGuid(), space, expectedWidth, expectedHeight, format, bytes);
    }
}
