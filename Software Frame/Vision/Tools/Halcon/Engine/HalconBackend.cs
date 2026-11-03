using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Halcon.Engine;

internal interface IHalconVisionRuntime : IDisposable
{
    Exception? CleanupFailure
    {
        get;
    }
    IHalconShapeModel Prepare(TemplateModelDefinition definition);
    BinaryImageOutput Threshold(ThresholdRangeOperation operation);
    GrayStatisticsOutput Statistics(GrayStatisticsOperation operation);
    ImageCropOutput Crop(ImageCropOperation operation);
}
internal interface IHalconShapeModel : IPreparedVisionModel
{
    IReadOnlyList<HalconShapeObservation> Run(TemplateMatchOperation operation);
}
internal sealed record HalconShapeObservation(double Row, double Column, double AngleRadians, double Scale, double Score);

/// <summary>新入口的HALCON形状匹配；直接SDK实现，不调用旧ShapeMatchTool或Session。</summary>
internal sealed class HalconBackend : IVisionBackend
{
    private readonly IHalconVisionRuntime _runtime;
    private bool _disposed;
    public string Id => "halcon";
    public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public IReadOnlyList<VisionOperationCapability> Capabilities
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        new VisionOperationCapability(TemplateMatchOperation.Id, VisionExecutionDomain.Vision,
            VisionOperationFeatures.Rotation | VisionOperationFeatures.UniformScale | VisionOperationFeatures.TemplateMask |
            VisionOperationFeatures.MultipleMatches | VisionOperationFeatures.IgnorePolarity | VisionOperationFeatures.NativeAutomaticSearch),
        new VisionOperationCapability(ThresholdRangeOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(GrayStatisticsOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(ImageCropOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)
    });

    internal HalconBackend() : this(new HalconVisionRuntime()) { }
    internal HalconBackend(IHalconVisionRuntime runtime) => _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var input = operation.Image;
        if (input == null)
        {
            return [new("UnsupportedOperation", "此SDK后端只执行具有真实图像输入的操作。")];
        }
        List<VisionIssue> issues = [];
        if (_runtime.CleanupFailure != null)
        {
            issues.Add(new("ResourceCleanupFailed", "HALCON存在待清理资源，必须关闭引擎。"));
        }

        if (operation is ThresholdRangeOperation or GrayStatisticsOperation or ImageCropOperation)
        {
            if (input.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                issues.Add(new("UnsupportedPixelFormat", "灰度处理、统计与裁剪只接收Mono8/Mono16，不自动转换格式。"));
            }

            return issues.AsReadOnly();
        }
        if (operation is not TemplateMatchOperation match)
        {
            return [new("UnsupportedOperation", "HALCON当前只接通形状匹配、区间阈值、灰度统计与矩形裁剪。")];
        }

        if (match.Image.Format != PixelFormat.Mono8)
        {
            issues.Add(new("UnsupportedPixelFormat", "形状搜索只接收Mono8，不隐式降位深或转换颜色。"));
        }

        if (match.Model!.Definition is not TemplateModelDefinition definition || definition.Metric != TemplateMatchMetric.ShapeEdges)
        {
            issues.Add(new("UnsupportedMetric", "HALCON首批仅接ShapeEdges，不能拿轮廓分数冒充灰度相关。"));
        }
        else if (!HalconShapeCoordinates.TryNativeRange(definition.PreparedRange, match.SearchRange, out _))
        {
            issues.Add(new("UnsupportedAngleRange", "本批不拆分跨原生模型训练边界的圆周子域，请按本次搜索范围重新准备模型。"));
        }

        if (!match.Sampling.UsesNativeSearch)
        {
            issues.Add(new("UnsupportedSampling", "请选择显式NativeAutomatic；HALCON金字塔/精化不能冒充指定离散网格。"));
        }

        if (match.SearchMask != null)
        {
            issues.Add(new("UnsupportedSearchMask", "自定义锚点的搜索掩膜尚未适配，不把HALCON默认质心域当锚点域。"));
        }

        if (match.MaximumMatches > 100)
        {
            issues.Add(new("UnsupportedMatchCount", "首批最多返回100个目标。"));
        }

        if (model is not IHalconShapeModel)
        {
            issues.Add(new("UnsupportedModel", "需要当前上下文的HALCON形状模型。"));
        }

        return issues.AsReadOnly();
    }

    public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken cancellationToken)
        => new(Task.Run<IPreparedVisionModel>(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (definition is not TemplateModelDefinition template || template.Metric != TemplateMatchMetric.ShapeEdges || template.Template.Format != PixelFormat.Mono8)
            {
                throw new NotSupportedException("HALCON首批只准备Mono8的ShapeEdges模板。");
            }

            return _runtime.Prepare(template); // 返回后由共享引擎观察取消，候选绝不失去所有权。
        }));

    public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken cancellationToken)
        => new(Task.Run(() =>
        {
            var issues = Validate(operation, model);
            if (issues.Count != 0)
            {
                throw new NotSupportedException(issues[0].Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (operation is ImageCropOperation crop)
            {
                var result = _runtime.Crop(crop);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(result);
            }

            if (operation is GrayStatisticsOperation statistics)
            {
                var result = _runtime.Statistics(statistics);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(result);
            }
            if (operation is ThresholdRangeOperation threshold)
            {
                var binary = _runtime.Threshold(threshold);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(binary);
            }
            var match = (TemplateMatchOperation)operation;
            var definition = (TemplateModelDefinition)match.Model!.Definition;
            var observations = ((IHalconShapeModel)model!).Run(match);
            cancellationToken.ThrowIfCancellationRequested();
            if (observations.Count > match.MaximumMatches)
            {
                throw new InvalidDataException("HALCON返回数量超出本次请求。");
            }

            var candidates = observations.Select(item => HalconShapeCoordinates.ToCandidate(item, definition.Origin)).ToArray();
            // HALCON精化可能越过请求边界，不能丢弃首N中的非法项后假称找齐有效目标。
            if (candidates.Any(item => item.Score < match.MinimumScore ||
                !match.SearchRange.Contains(new(item.AngleDegrees, 0, item.Scale, item.Scale))))
            {
                throw new InvalidDataException("HALCON精化结果越过本次角度/尺度/分数边界；本次不发布部分结果。");
            }

            var output = new TemplateMatchOutput(match.Image.FrameId, match.Image.CoordinateSpaceId,
                "HALCON scaled shape model native pyramid/least_squares; visible edge support, not gray correlation", candidates.OrderByDescending(item => item.Score));
            return new BackendOutput(output, candidates.Length == 0);
        }));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _runtime.Dispose();
        _disposed = true;
    }
}

internal static class HalconShapeCoordinates
{
    internal static bool TryNativeRange(TemplateSearchRange prepared, TemplateSearchRange search,
        out (double Start, double Extent) range)
    {
        var preparedStart = Pose2D.NormalizeAngle(-prepared.AngleStartDegrees - prepared.AngleExtentDegrees);
        var searchStart = Pose2D.NormalizeAngle(-search.AngleStartDegrees - search.AngleExtentDegrees);
        var offset = (searchStart - preparedStart + 360) % 360;
        range = ((preparedStart + offset) * Math.PI / 180, search.AngleExtentDegrees * Math.PI / 180);
        return prepared.Contains(search) && offset + search.AngleExtentDegrees <= prepared.AngleExtentDegrees + 1e-10;
    }

    internal static TemplateMatchCandidate ToCandidate(HalconShapeObservation native, Point2D origin)
    {
        var degrees = Pose2D.NormalizeAngle(-native.AngleRadians * 180 / Math.PI);
        // HALCON23.11官方例：vector_angle_to_rigid + hom_mat2d_scale + affine_trans_pixel(0,0)。
        // affine_trans_pixel先+0.5、变换后-0.5；Row/Column不是像素中心锚点，不能直接交换当X/Y。
        var halfPixel = new Pose2D(0, 0, degrees).Transform(new(0.5 * native.Scale, 0.5 * native.Scale));
        return new(new(native.Column + halfPixel.X - 0.5, native.Row + halfPixel.Y - 0.5),
            origin, degrees, native.Scale, native.Score);
    }
}
