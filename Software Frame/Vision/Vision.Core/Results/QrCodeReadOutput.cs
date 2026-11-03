#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 单目标标准QR快照，文本为原生返回的UTF-8解码字符串，不声明原始字节/ECI/GS1/空载荷或质量等级。
/// 四角为原图像素中心坐标的原生循环顺序，不冒充有固定图像朝向的左上角，不加半像素或整数覆盖框。
/// </summary>
public sealed class QrCodeReadOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public QrCodeReadState State
    {
        get;
    }

    public string? Text
    {
        get;
    }

    public IReadOnlyList<Point2D> Corners
    {
        get;
    }

    /// <summary>本次单目标读取的数量，不是多码场景中的全部码数。</summary>
    public int DecodedCount => State == QrCodeReadState.Decoded ? 1 : 0;
    public int DetectedCount => State == QrCodeReadState.NotDetected ? 0 : 1;

    public QrCodeReadOutput(Guid frameId, Guid coordinateSpaceId, QrCodeReadState state, string? text, IEnumerable<Point2D> corners)
    {
        ArgumentNullException.ThrowIfNull(corners);
        var snapshot = corners.Take(5).ToArray();
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || !Enum.IsDefined(state)
            || text is { Length: > 8192 } || (state == QrCodeReadState.Decoded ? string.IsNullOrEmpty(text) : text != null)
            || (state == QrCodeReadState.NotDetected ? snapshot.Length != 0 : !ValidQuadrilateral(snapshot)))
        {
            throw new ArgumentException("QR状态、文本、数量或四角不一致，不能以检测框伪装成功解码。");
        }
        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        State = state;
        Text = text;
        Corners = Array.AsReadOnly(snapshot);
    }

    private static bool ValidQuadrilateral(Point2D[] points)
    {
        if (points.Length != 4 || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            return false;
        }
        double? sign = null;
        for (var i = 0; i < 4; i++)
        {
            var first = points[i];
            var second = points[(i + 1) % 4];
            var third = points[(i + 2) % 4];
            var cross = (second.X - first.X) * (third.Y - second.Y) - (second.Y - first.Y) * (third.X - second.X);
            if (!double.IsFinite(cross) || cross == 0 || sign != null && Math.Sign(cross) != sign)
            {
                return false;
            }
            sign = Math.Sign(cross);
        }
        return true;
    }
}
