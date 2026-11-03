using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static QrCodeReadOutput ReadSingleQr(QrCodeReadOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var window = new Mat(source, new Rect(operation.X, operation.Y, operation.Width, operation.Height));
        // 每次调用独占读取器及视图，原生返回前不释放；不拿取消等待当成检测器停止。
        using var detector = new QRCodeDetector();
        var text = detector.DetectAndDecode(window, out Point2f[] corners);
        if (corners.Length == 0)
        {
            if (!string.IsNullOrEmpty(text))
            {
                throw new InvalidDataException("原生QR返回文本却没有四角，不能发布无来源几何的成功结果。");
            }
            return new(frame.FrameId, frame.CoordinateSpaceId, QrCodeReadState.NotDetected, null, []);
        }
        if (corners.Length != 4)
        {
            throw new InvalidDataException("单目标QR返回了非四角几何，不能裁取部分结果。");
        }
        var points = corners.Select(point => new Point2D((double)point.X + operation.X, (double)point.Y + operation.Y));
        var decoded = !string.IsNullOrEmpty(text);
        return new(frame.FrameId, frame.CoordinateSpaceId,
            decoded ? QrCodeReadState.Decoded : QrCodeReadState.DetectedUndecodable, decoded ? text : null, points);
    }
}
