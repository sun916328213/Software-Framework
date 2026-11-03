using OpenCvSharp;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;
using Vision.Tools.OpenCv.Imaging;
using Vision.Tools.OpenCv.Matching;

namespace Vision.Tools.OpenCv.Engine;

/// <summary>
/// 选中后才激活的 OpenCV 实现。直接使用新 Core 请求和原生算法，不依赖工具会话或旧图像类型。
/// </summary>
internal sealed partial class OpenCvBackend : IVisionBackend
{
    private bool _disposed;

    public string Id => "opencv";
    public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public IReadOnlyList<VisionOperationCapability> Capabilities
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        new VisionOperationCapability(TemplateMatchOperation.Id, VisionExecutionDomain.Vision,
            VisionOperationFeatures.Rotation | VisionOperationFeatures.UniformScale | VisionOperationFeatures.TemplateMask
            | VisionOperationFeatures.SearchMask | VisionOperationFeatures.MultipleMatches),
        new VisionOperationCapability(ThresholdRangeOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(RgbColorSegmentOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(HsvColorSegmentOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(AlignedImageDifferenceOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(QrCodeReadOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(QrCodeReadManyOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.MultipleMatches),
        new VisionOperationCapability(GrayStatisticsOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(GrayHistogramOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(ImageCropOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(ImageFileOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(ImageConvertOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(ColorChannelExtractOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(GrayLinearOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(GrayGammaOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(GrayNormalizeOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(ImageFilterOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(OtsuThresholdOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(AdaptiveThresholdOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(MorphologyOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(BlobOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion),
        new VisionOperationCapability(CaliperLineOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(CaliperEdgePairOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
        new VisionOperationCapability(CaliperCircleOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)
    });

    internal OpenCvBackend()
    {
        // 实际运行库检查；异常向装配者传播，不回退其他实现。
        _ = Cv2.GetVersionString();
    }

    public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (operation is ImageFileOperation)
        {
            return [];
        }

        if (operation is ColorChannelExtractOperation channel)
        {
            if (channel.Image.Format is not (PixelFormat.Rgb24 or PixelFormat.Bgr24 or PixelFormat.Bgra32))
            {
                return [new("UnsupportedPixelFormat", "RGB通道提取只接收Rgb24/Bgr24/Bgra32，不猜灰度或浮点的通道含义。")];
            }

            return (long)channel.Image.Width * channel.Image.Height <= MaximumColorImagePixels
                ? [] : [new("ImageBudgetExceeded", "通道提取输入最多2000万像素；不自动裁剪、缩放或截断。")];
        }

        if (operation is ImageConvertOperation conversion)
        {
            return SupportsConversion(conversion.Image.Format, conversion.OutputFormat)
                ? [] : [new("UnsupportedConversion", "此格式组合未接通；Mono16到Mono8须显式指定GrayLinear的gain和offset。")];
        }

        if (operation is QrCodeReadManyOperation many)
        {
            if (many.Image.Format != PixelFormat.Mono8)
            {
                return [new("UnsupportedPixelFormat", "多QR只接Mono8，不隐式转换颜色/位深或归一化。")];
            }
            return (long)many.Image.Width * many.Image.Height <= 20_000_000 && (long)many.Width * many.Height <= 4_000_000
                && many.Width < 32767 && many.Height < 32767
                ? [] : [new("ImageBudgetExceeded", "多QR输入最多2000万像素，单窗最多400万且每边小于32767。")];
        }

        if (operation is QrCodeReadOperation qr)
        {
            if (qr.Image.Format != PixelFormat.Mono8)
            {
                return [new("UnsupportedPixelFormat", "单目标QR只接Mono8，须先显式转换位深或颜色。")];
            }
            return (long)qr.Image.Width * qr.Image.Height <= 20_000_000 && (long)qr.Width * qr.Height <= 4_000_000
                && qr.Width < 32767 && qr.Height < 32767
                ? [] : [new("ImageBudgetExceeded", "QR输入最多2000万像素，单窗最多400万像素且每边须小于32767。")];
        }

        if (operation is AlignedImageDifferenceOperation difference)
        {
            if (difference.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "对齐双图差分只接收同位深Mono8/Mono16，不隐式转灰或猜浮点范围。")];
            }
            return (long)difference.Image.Width * difference.Image.Height <= MaximumDifferenceImagePixels
                ? [] : [new("ImageBudgetExceeded", "双图差分每张输入不得超过2000万像素，须先显式裁剪并对齐。")];
        }

        if (operation is HsvColorSegmentOperation hsv)
        {
            if (hsv.Image.Format is not (PixelFormat.Rgb24 or PixelFormat.Bgr24))
            {
                return [new("UnsupportedPixelFormat", "HSV8分割只接收Rgb24/Bgr24，不猜灰度/浮点颜色或丢弃alpha。")];
            }
            return (long)hsv.Image.Width * hsv.Image.Height <= MaximumColorImagePixels
                ? [] : [new("ImageBudgetExceeded", "HSV8分割输入超过2000万像素，须先显式裁剪并处理来源。")];
        }

        if (operation is RgbColorSegmentOperation color)
        {
            if (color.Image.Format is not (PixelFormat.Rgb24 or PixelFormat.Bgr24))
            {
                return [new("UnsupportedPixelFormat", "RGB颜色分割只接收Rgb24/Bgr24，不隐式转换灰度、位深或忽略alpha。")];
            }
            return (long)color.Image.Width * color.Image.Height <= MaximumColorImagePixels
                ? [] : [new("ImageBudgetExceeded", "RGB分割输入超过2000万像素，请显式裁剪并处理来源映射。")];
        }

        if (operation is GrayHistogramOperation histogram)
        {
            if (histogram.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "灰度直方图只接收Mono8/Mono16，不隐式转灰或量化浮点。")];
            }

            return (long)histogram.Image.Width * histogram.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "直方图输入最多2000万像素，须先显式裁剪并处理来源。")];
        }

        if (operation is GrayNormalizeOperation normalize)
        {
            if (normalize.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "灰度归一化只处理Mono8/Mono16。")];
            }

            return (long)normalize.Image.Width * normalize.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "灰度归一化输入最多2000万像素。")];
        }

        if (operation is GrayGammaOperation gamma)
        {
            if (gamma.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "灰度Gamma只处理Mono8/Mono16，不隐式转灰、量化浮点或解释alpha。")];
            }

            return (long)gamma.Image.Width * gamma.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "Gamma输入最多2000万像素，不自动裁剪、缩放或截断。")];
        }

        if (operation is GrayLinearOperation linear)
        {
            if (linear.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "灰度线性变换只处理Mono8/Mono16，不隐式转灰或解释浮点范围。")];
            }
            return linear.Rounding != GrayLinearRounding.HalfUp || (long)linear.Image.Width * linear.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "半向上线性变换输入最多2000万像素。")];
        }

        if (operation is ImageFilterOperation filter)
        {
            if (filter.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "灰度滤波只支持Mono8/Mono16，不隐式转换颜色或位深。")];
            }

            if (filter.Kind == ImageFilterKind.Median
                && (filter.Border != ImageBorderMode.Replicate
                    || filter.Image.Format == PixelFormat.Mono16 && filter.KernelSize > 5))
            {
                return [new("UnsupportedFilterCombination", "OpenCV中值滤波只支持复制边界，Mono16核只能为3或5。")];
            }

            return [];
        }

        if (operation is OtsuThresholdOperation otsu)
        {
            return otsu.Image.Format is PixelFormat.Mono8 or PixelFormat.Mono16
                ? [] : [new("UnsupportedPixelFormat", "Otsu只支持Mono8/Mono16，不自动转灰或降位深。")];
        }

        if (operation is AdaptiveThresholdOperation adaptive)
        {
            return adaptive.Image.Format == PixelFormat.Mono8
                ? [] : [new("UnsupportedPixelFormat", "自适应阈值只支持Mono8，不猜测16位到8位的映射。")];
        }

        if (operation is MorphologyOperation morphology)
        {
            return morphology.Image.Format == PixelFormat.Mono8
                ? [] : [new("UnsupportedPixelFormat", "二值形态学只支持Mono8的0/255数据。")];
        }

        if (operation is CaliperCircleOperation circle)
        {
            if (circle.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "圆卡尺只接收Mono8/Mono16，不归一化或自动转灰。")];
            }

            return circle.Image.Width < 32767 && circle.Image.Height < 32767
                && (long)circle.Image.Width * circle.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "圆卡尺图像须每边小于32767且总像素不超过2000万。")];
        }

        if (operation is CaliperEdgePairOperation pair)
        {
            if (pair.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "边缘对只支持Mono8/Mono16，不自动转灰或归一化。")];
            }

            return pair.Image.Width < 32767 && pair.Image.Height < 32767
                && (long)pair.Image.Width * pair.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "边缘对图像须每边小于32767且总像素不超过2000万。")];
        }

        if (operation is CaliperLineOperation caliper)
        {
            if (caliper.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "卡尺只支持Mono8/Mono16，不自动转灰或归一化。")];
            }

            return caliper.Image.Width < 32767 && caliper.Image.Height < 32767
                && (long)caliper.Image.Width * caliper.Image.Height <= 20_000_000
                ? [] : [new("ImageBudgetExceeded", "卡尺图像须每边小于32767且总像素不超过2000万，请显式裁剪。")];
        }

        if (operation is BlobOperation blob)
        {
            if (blob.Image.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
            {
                return [new("UnsupportedPixelFormat", "Blob只接收Mono8/Mono16，不隐式转换颜色。")];
            }

            return (long)blob.Image.Width * blob.Image.Height <= MaximumBlobImagePixels
                ? [] : [new("ImageBudgetExceeded", "Blob输入超过2000万像素，请显式裁剪并处理来源映射。")];
        }

        var input = operation.Image;
        if (input == null)
        {
            return [new("UnsupportedOperation", "此SDK后端只执行具有真实图像输入的操作。")];
        }
        if (operation is ImageCropOperation)
        {
            return input.Format is PixelFormat.Mono8 or PixelFormat.Mono16 or PixelFormat.Rgb24
                or PixelFormat.Bgr24 or PixelFormat.Bgra32 or PixelFormat.Float32
                ? [] : [new("UnsupportedPixelFormat", "裁剪不支持此像素格式。")];
        }
        if (operation is ThresholdRangeOperation or GrayStatisticsOperation)
        {
            return input.Format is PixelFormat.Mono8 or PixelFormat.Mono16
                ? [] : [new("UnsupportedPixelFormat", "灰度处理与统计只接收Mono8/Mono16，不自动转换格式。")];
        }

        List<VisionIssue> issues = [];
        if (operation is not TemplateMatchOperation match)
        {
            return [new("UnsupportedOperation", "OpenCV未接通此操作；不会借用其他厂商或隐式变更处理方式。")];
        }

        if (match.Image.Format != PixelFormat.Mono8)
        {
            issues.Add(new("UnsupportedPixelFormat", "模板搜索输入必须为Mono8；不隐式转换位深或颜色。"));
        }

        if (match.Model!.Definition is not TemplateModelDefinition definition
            || definition.Metric != TemplateMatchMetric.GrayCorrelation || definition.IgnorePolarity)
        {
            issues.Add(new("UnsupportedMetric", "只支持保持极性的归一化灰度相关，不冒充形状匹配。"));
        }

        if (match.Sampling.UsesNativeSearch)
        {
            issues.Add(new("UnsupportedSampling", "OpenCV当前只实现显式离散角度/缩放采样。"));
        }

        if (TemplateMatchSearch.PoseSampleCount(match) > TemplateMatchSearch.MaximumPoseSamples)
        {
            issues.Add(new("SearchBudgetExceeded", "姿态采样超过1024，请缩小范围或明确增大采样步长。"));
        }

        if (match.MaximumMatches > TemplateMatchSearch.MaximumMatches)
        {
            issues.Add(new("SearchBudgetExceeded", "单次最多返回100个目标。"));
        }

        if (model is not PreparedTemplate prepared || prepared.IsDisposed)
        {
            issues.Add(new("UnsupportedModel", "需要当前上下文中尚未释放的OpenCV灰度模板。"));
        }

        return issues.AsReadOnly();
    }

    public ValueTask<IPreparedVisionModel> PrepareAsync(
        VisionModelDefinition definition,
        CancellationToken cancellationToken)
    {
        // 不传 Task.Run 调度取消标志：一旦启动，必须等待实际调用及清理完成。
        return new(Task.Run<IPreparedVisionModel>(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (definition is not TemplateModelDefinition template
                || template.Metric != TemplateMatchMetric.GrayCorrelation || template.IgnorePolarity)
            {
                throw new NotSupportedException("OpenCV只准备保持极性的灰度相关模板。");
            }

            if (template.Template.Format != PixelFormat.Mono8)
            {
                throw new NotSupportedException("模板必须为Mono8。");
            }

            var native = OpenCvFrameAdapter.CopyGray(template.Template);
            try
            {
                using var mask = template.Mask == null ? null : TemplateMatchSearch.MaskMat(template.Mask);
                if (mask != null && Cv2.CountNonZero(mask) < 4)
                {
                    throw new ArgumentException("模板有效像素至少为4个。", nameof(definition));
                }

                Scalar deviation;
                if (mask == null)
                {
                    Cv2.MeanStdDev(native, out Scalar _, out deviation);
                }
                else
                {
                    Cv2.MeanStdDev(native, out Scalar _, out deviation, mask);
                }
                if (deviation.Val0 <= 1e-6)
                {
                    throw new ArgumentException("模板有效域没有足够灰度变化。", nameof(definition));
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new PreparedTemplate(native);
            }
            catch
            {
                native.Dispose();
                throw;
            }
        }));
    }

    public ValueTask<BackendOutput> ExecuteAsync(
        IPreparedVisionModel? model,
        VisionOperation operation,
        CancellationToken cancellationToken)
    {
        return new(Task.Run(() =>
        {
            var issues = Validate(operation, model);
            if (issues.Count != 0)
            {
                throw new NotSupportedException(issues[0].Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (operation is ImageFileOperation file)
            {
                var output = ReadFile(file, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is ColorChannelExtractOperation channel)
            {
                var output = ExtractColorChannel(channel);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is ImageConvertOperation conversion)
            {
                var output = ConvertImage(conversion);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is QrCodeReadManyOperation many)
            {
                var output = ReadManyQr(many, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output, output.DecodedCount == 0);
            }

            if (operation is QrCodeReadOperation qr)
            {
                var output = ReadSingleQr(qr);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output, output.State != QrCodeReadState.Decoded);
            }

            if (operation is AlignedImageDifferenceOperation difference)
            {
                var output = Difference(difference);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is HsvColorSegmentOperation hsv)
            {
                var output = SegmentHsv(hsv);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is RgbColorSegmentOperation color)
            {
                var output = SegmentRgb(color);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is GrayHistogramOperation histogram)
            {
                var output = Histogram(histogram, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is GrayNormalizeOperation normalize)
            {
                var output = NormalizeGray(normalize, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is GrayGammaOperation gamma)
            {
                var output = GammaGray(gamma, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is GrayLinearOperation linear)
            {
                var output = linear.Rounding == GrayLinearRounding.HalfUp
                    ? LinearGrayHalfUp(linear, cancellationToken) : LinearGray(linear);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is ImageFilterOperation filter)
            {
                var output = Filter(filter);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is OtsuThresholdOperation otsu)
            {
                var output = Otsu(otsu);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is AdaptiveThresholdOperation adaptive)
            {
                var output = Adaptive(adaptive);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is MorphologyOperation morphology)
            {
                var output = Morphology(morphology);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is CaliperCircleOperation circle)
            {
                var output = FindCaliperCircle(circle, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output, output.Circle == null);
            }

            if (operation is CaliperEdgePairOperation pair)
            {
                var output = MeasureEdgePairs(pair, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output, output.Statistics == null);
            }

            if (operation is CaliperLineOperation caliper)
            {
                var output = FindCaliperLine(caliper, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output, output.Line == null);
            }

            if (operation is BlobOperation blob)
            {
                var output = FindBlobs(blob, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output, output.Items.Count == 0);
            }

            if (operation is ImageCropOperation crop)
            {
                var output = Crop(crop);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is GrayStatisticsOperation statistics)
            {
                var output = Statistics(statistics);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            if (operation is ThresholdRangeOperation threshold)
            {
                var output = Threshold(threshold);
                cancellationToken.ThrowIfCancellationRequested();
                return new BackendOutput(output);
            }

            var match = (TemplateMatchOperation)operation;
            var prepared = (PreparedTemplate)model!;
            using var source = OpenCvFrameAdapter.CopyGray(match.Image);
            var candidates = TemplateMatchSearch.Search(source, prepared.Native, match, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new BackendOutput(new TemplateMatchOutput(match.Image.FrameId, match.Image.CoordinateSpaceId,
                "OpenCV CCoeffNormed, binary effective domain; discrete angle/scale sampling", candidates), candidates.Count == 0);
        }));
    }

    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>仅上下文拥有此模板资源；公开模型引用不持有原生句柄。</summary>
    private sealed class PreparedTemplate(Mat native) : IPreparedVisionModel
    {
        internal Mat Native { get; } = native;
        internal bool IsDisposed
        {
            get; private set;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            Native.Dispose();
            IsDisposed = true;
        }
    }
}
