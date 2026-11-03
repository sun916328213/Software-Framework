using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    /// <summary>两次扫描原始整数编码；padding不参与极值。使用整数算术实现半向上，常量图原样复制。</summary>
    private static GrayNormalizeOutput NormalizeGray(GrayNormalizeOperation operation, CancellationToken token)
    {
        var frame = operation.Image;
        var sixteen = frame.Format == PixelFormat.Mono16;
        var bpp = sixteen ? 2 : 1;
        var rowBytes = checked(frame.Width * bpp);
        var maximum = sixteen ? ushort.MaxValue : byte.MaxValue;
        var min = (int)maximum;
        var max = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            var row = frame.Pixels.Slice(y * frame.Stride, rowBytes);
            for (var x = 0; x < frame.Width; x++)
            {
                if ((x & 4095) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }
                var value = sixteen ? BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(x * 2, 2)) : row[x];
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
        }

        var pixels = new byte[checked(rowBytes * frame.Height)];
        var span = max - min;
        for (var y = 0; y < frame.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            var source = frame.Pixels.Slice(y * frame.Stride, rowBytes);
            var target = pixels.AsSpan(y * rowBytes, rowBytes);
            for (var x = 0; x < frame.Width; x++)
            {
                if ((x & 4095) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }
                var value = sixteen ? BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(x * 2, 2)) : source[x];
                var mapped = span == 0 ? value : (int)(((long)(value - min) * maximum * 2 + span) / (2L * span));
                if (sixteen)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(x * 2, 2), (ushort)mapped);
                }
                else
                {
                    target[x] = (byte)mapped;
                }
            }
        }
        token.ThrowIfCancellationRequested();
        var image = new VisionFrame(Guid.NewGuid(), frame.CoordinateSpaceId, frame.Width, frame.Height, frame.Format, pixels);
        return new(frame.FrameId, image, min, max);
    }
}
