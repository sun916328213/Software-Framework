using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    /// <summary>
    /// 选中模块内的托管整数计数内核，不宣称调用SDK CalcHist。
    /// 完整原位深计数用long，避免浮点直方图在大数量时丢失整数精度；最多65536箱。
    /// </summary>
    private static GrayHistogramOutput Histogram(GrayHistogramOperation operation, CancellationToken ct)
    {
        var frame = operation.Image;
        var levels = frame.Format == PixelFormat.Mono8 ? 256 : 65536;
        var raw = new long[levels];
        if (operation.Region == null)
        {
            for (var row = 0; row < frame.Height; row++)
            {
                AccumulateHistogram(frame, row, 0, frame.Width - 1, raw, ct);
            }
        }
        else
        {
            foreach (var run in operation.Region.Runs(frame.Width, frame.Height))
            {
                AccumulateHistogram(frame, run.Row, run.Start, run.End, raw, ct);
            }
        }

        ct.ThrowIfCancellationRequested();
        var count = raw.Sum();
        if (count == 0)
        {
            throw new InvalidOperationException("直方图参与域没有有效像素，不发布空计数中位数。");
        }

        var grouped = new long[(levels + operation.BinWidth - 1) / operation.BinWidth];
        var lowerRank = (count - 1) / 2;
        var upperRank = count / 2;
        var lower = -1;
        var upper = -1;
        long cumulative = 0;
        for (var gray = 0; gray < raw.Length; gray++)
        {
            grouped[gray / operation.BinWidth] += raw[gray];
            cumulative += raw[gray];
            if (lower < 0 && cumulative > lowerRank)
            {
                lower = gray;
            }

            if (upper < 0 && cumulative > upperRank)
            {
                upper = gray;
            }
        }

        ct.ThrowIfCancellationRequested();
        return new(frame.FrameId, frame.CoordinateSpaceId, frame.Format, operation.BinWidth, grouped, (lower + upper) / 2d);
    }

    private static void AccumulateHistogram(VisionFrame frame, int row, int start, int end, long[] bins, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var pixels = frame.Pixels.Slice(row * frame.Stride, frame.Stride);
        var mono16 = frame.Format == PixelFormat.Mono16;
        for (var column = start; column <= end; column++)
        {
            // 即使图像只有一条很长的行，也周期性响应取消；结果统一在引擎发布前复查。
            if ((column & 4095) == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

            var gray = mono16
                ? BinaryPrimitives.ReadUInt16LittleEndian(pixels.Slice(column * 2, 2))
                : pixels[column];
            bins[gray]++;
        }
    }
}
