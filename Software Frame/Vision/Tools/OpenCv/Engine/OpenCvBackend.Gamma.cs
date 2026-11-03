using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    /// <summary>
    /// 选中模块内的托管全灰度LUT内核，不宣称调用SDK Gamma指令。
    /// 输入按原编码满量程归一到[0,1]作幂函数，不按本幅图像的极值拉伸。
    /// </summary>
    private static ImageTransformOutput GammaGray(GrayGammaOperation operation, CancellationToken ct)
    {
        var frame = operation.Image;
        var mono16 = frame.Format == PixelFormat.Mono16;
        var maximum = mono16 ? ushort.MaxValue : byte.MaxValue;
        var table = new ushort[maximum + 1];
        for (var gray = 1; gray < maximum; gray++)
        {
            if ((gray & 4095) == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

            var corrected = maximum * Math.Pow(gray / (double)maximum, operation.Gamma);
            table[gray] = (ushort)Math.Floor(corrected + 0.5);
        }
        // 对所有有限正指数，两个端点固定；不把gamma取倒数或自动反相。
        table[0] = 0;
        table[maximum] = maximum;
        ct.ThrowIfCancellationRequested();
        var bpp = mono16 ? 2 : 1;
        var rowBytes = checked(frame.Width * bpp);
        var pixels = new byte[checked(rowBytes * frame.Height)];
        for (var row = 0; row < frame.Height; row++)
        {
            ct.ThrowIfCancellationRequested();
            var source = frame.Pixels.Slice(row * frame.Stride, rowBytes);
            var target = pixels.AsSpan(row * rowBytes, rowBytes);
            for (var column = 0; column < frame.Width; column++)
            {
                if ((column & 4095) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                }

                if (mono16)
                {
                    var value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(column * 2, 2));
                    BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(column * 2, 2), table[value]);
                }
                else
                {
                    target[column] = (byte)table[source[column]];
                }
            }
        }

        ct.ThrowIfCancellationRequested();
        return new(frame.FrameId, new VisionFrame(Guid.NewGuid(), frame.CoordinateSpaceId,
            frame.Width, frame.Height, frame.Format, pixels));
    }
}
