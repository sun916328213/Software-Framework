using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    /// <summary>显式半向上采用托管逐像素内核，先限幅再量化；不改变既有SDK默认路径。</summary>
    private static ImageTransformOutput LinearGrayHalfUp(GrayLinearOperation operation, CancellationToken token)
    {
        var frame = operation.Image;
        var input16 = frame.Format == PixelFormat.Mono16;
        var output16 = operation.OutputFormat == PixelFormat.Mono16;
        var inputBytes = input16 ? 2 : 1;
        var outputBytes = output16 ? 2 : 1;
        var outputStride = checked(frame.Width * outputBytes);
        var pixels = new byte[checked(outputStride * frame.Height)];
        var maximum = output16 ? ushort.MaxValue : byte.MaxValue;
        for (var y = 0; y < frame.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            var row = frame.Pixels.Slice(y * frame.Stride, checked(frame.Width * inputBytes));
            var output = pixels.AsSpan(y * outputStride, outputStride);
            for (var x = 0; x < frame.Width; x++)
            {
                if ((x & 4095) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }
                var value = input16 ? BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(x * 2, 2)) : row[x];
                var mapped = Math.Clamp(operation.Gain * value + operation.Offset, 0, maximum);
                // 不直接加0.5：二进制浮点在中点前一位可能被加法提前舍入到整数。
                var rounded = (ushort)Math.Round(mapped, MidpointRounding.AwayFromZero);
                if (output16)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(x * 2, 2), rounded);
                }
                else
                {
                    output[x] = (byte)rounded;
                }
            }
        }
        token.ThrowIfCancellationRequested();
        return new(frame.FrameId, new VisionFrame(Guid.NewGuid(), frame.CoordinateSpaceId,
            frame.Width, frame.Height, operation.OutputFormat, pixels));
    }
}
