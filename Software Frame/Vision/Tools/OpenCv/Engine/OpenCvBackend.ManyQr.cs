using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static QrCodeReadManyOutput ReadManyQr(QrCodeReadManyOperation operation, CancellationToken cancellationToken)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var window = new Mat(source, new Rect(operation.X, operation.Y, operation.Width, operation.Height));
        using var detector = new QRCodeDetector();
        var detected = detector.DetectMulti(window, out Point2f[] points);
        cancellationToken.ThrowIfCancellationRequested();
        if (!detected)
        {
            if (points.Length != 0)
            {
                throw new InvalidDataException("多QR未检测状态却返回了候选四角，不能发布矛盾快照。");
            }
            return new(frame.FrameId, frame.CoordinateSpaceId, []);
        }
        if (points.Length == 0 || points.Length % 4 != 0)
        {
            throw new InvalidDataException("多QR原生候选四角数量无效，不能截取部分点伪造完整结果。");
        }
        if (points.Length / 4 > operation.MaximumCandidates)
        {
            throw new InvalidOperationException("多QR原生候选超过本次预算，未截断、解码或发布部分计数。");
        }

        var candidates = new List<QrCodeReadOutput>();
        for (var index = 0; index < points.Length; index += 4)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var corners = points.Skip(index).Take(4).ToArray();
            // 逐候选Decode使空文本与对应四角严格关联；不丢弃未解码候选或按相同文本去重。
            var text = detector.Decode(window, corners);
            cancellationToken.ThrowIfCancellationRequested();
            var decoded = !string.IsNullOrEmpty(text);
            candidates.Add(new(frame.FrameId, frame.CoordinateSpaceId,
                decoded ? QrCodeReadState.Decoded : QrCodeReadState.DetectedUndecodable, decoded ? text : null,
                corners.Select(point => new Point2D((double)point.X + operation.X, (double)point.Y + operation.Y))));
        }
        var ordered = candidates.OrderBy(candidate => candidate.Corners.Min(point => point.Y))
            .ThenBy(candidate => candidate.Corners.Min(point => point.X))
            .ThenBy(candidate => candidate.Corners.Max(point => point.Y))
            .ThenBy(candidate => candidate.Corners.Max(point => point.X));
        return new(frame.FrameId, frame.CoordinateSpaceId, ordered);
    }
}
