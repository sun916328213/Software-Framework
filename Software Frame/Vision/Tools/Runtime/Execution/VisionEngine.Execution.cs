using System.Diagnostics;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Common;

namespace Vision.Tools.Execution;

/// <summary>校验、执行与结果发布：只发布本次有效且来源一致的强类型结果。</summary>
internal sealed partial class VisionEngine
{
    public async ValueTask<IReadOnlyList<VisionIssue>> ValidateAsync(
        VisionContext context,
        VisionOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var lease = await EnterAsync(context, cancellationToken).ConfigureAwait(false);
        lease.Token.ThrowIfCancellationRequested();
        return Check(lease.State, operation).AsReadOnly();
    }

    public async ValueTask<VisionOutcome<TResult>> ExecuteAsync<TResult>(
        VisionContext context,
        VisionOperation<TResult> operation,
        CancellationToken cancellationToken)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var lease = await EnterAsync(context, cancellationToken).ConfigureAwait(false);
        // 从独占租约取得后起计；不计模型准备、调用前处理及上下文获取。
        var started = Stopwatch.GetTimestamp();
        try
        {
            var issues = Check(lease.State, operation);
            if (issues.Count > 0)
            {
                var issue = issues[0];
                return new(issue.Code.StartsWith("Unsupported", StringComparison.Ordinal)
                    ? VisionExecutionStatus.Unsupported : VisionExecutionStatus.Failed, null, issue, Stopwatch.GetElapsedTime(started));
            }
            lease.Token.ThrowIfCancellationRequested();
            BackendOutput output;
            if (CommonVisionOperations.Handles(operation))
            {
                output = CommonVisionOperations.Execute(operation);
            }
            else
            {
                var backend = operation.Domain == VisionExecutionDomain.Vision ? _vision : _ai!;
                output = await backend.ExecuteAsync(operation.Model == null ? null : lease.State.Active?.Resource,
                    operation, lease.Token).ConfigureAwait(false);
            }

            if (output.Value is not TResult result)
            {
                throw new InvalidOperationException("后端结果类型与操作契约不符。");
            }

            ValidateOutput(operation, output);
            // 真实调用返回后才作发布决定，仍持有上下文租约；不丢弃仍运行的SDK任务。
            lock (_gate)
            {
                ThrowIfClosing(lease.State, lease.Token);
                if (CheckLocation(operation) is { } dependency)
                {
                    return new(VisionExecutionStatus.Failed, null, dependency, Stopwatch.GetElapsedTime(started));
                }
                RememberLocation(operation, output);
            }
            return new(output.NoMatch ? VisionExecutionStatus.NoMatch : VisionExecutionStatus.Success, result, null, Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NotSupportedException error)
        {
            return new(VisionExecutionStatus.Unsupported, null, new("UnsupportedOperation", error.Message), Stopwatch.GetElapsedTime(started));
        }
        catch (Exception error)
        {
            return new(VisionExecutionStatus.Failed, null, new("ExecutionFailed", error.Message), Stopwatch.GetElapsedTime(started));
        }
    }

    private List<VisionIssue> Check(ContextState state, VisionOperation operation)
    {
        var issues = operation.Validate(Descriptor, state.Context).ToList();
        if (operation.Model != null && !ReferenceEquals(state.Active?.Reference, operation.Model))
        {
            issues.Add(new("ModelNotCurrent", "模型不是此上下文当前发布的原引用，可能已替换、释放或伪造。"));
        }

        if (state.CleanupFault != null)
        {
            issues.Add(new("ResourceCleanupFailed", "模型退役清理失败，资源仍被保留；请关闭并重建上下文。"));
        }

        if (CheckLocation(operation) is { } dependency)
        {
            issues.Add(dependency);
        }

        if (CommonVisionOperations.Handles(operation))
        {
            return issues;
        }

        var backend = operation.Domain == VisionExecutionDomain.Vision ? _vision : _ai;
        if (backend != null && issues.Count == 0)
        {
            issues.AddRange(backend.Validate(operation, operation.Model == null ? null : state.Active?.Resource));
        }

        return issues;
    }

    private static void ValidateOutput(VisionOperation operation, BackendOutput output)
    {
        if (CommonVisionOperations.Handles(operation))
        {
            CommonVisionOperations.ValidateOutput(operation, output);
            return;
        }

        if (operation is ImageFileOperation file)
        {
            if (output.Value is not ImageFileOutput decoded || output.NoMatch
                || !string.Equals(decoded.FilePath, file.FilePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("解码结果的文件来源或完成状态与本次请求不符。");
            }

            return;
        }

        var input = operation.Image ?? throw new InvalidOperationException("此图像结果必须具有真实图像输入。");
        var identity = output.Value switch
        {
            TemplateMatchOutput result => (result.FrameId, result.CoordinateSpaceId, result.Matches.Count),
            AiClassificationOutput result => (result.FrameId, result.CoordinateSpaceId, result.Predictions.Count),
            BinaryImageOutput result => (result.FrameId, result.Image.CoordinateSpaceId, 1),
            ImageDifferenceOutput result => (result.FrameId, result.Image.CoordinateSpaceId, 1),
            QrCodeReadOutput result => (result.FrameId, result.CoordinateSpaceId, result.DecodedCount),
            QrCodeReadManyOutput result => (result.FrameId, result.CoordinateSpaceId, result.DecodedCount),
            GrayStatisticsOutput result => (result.FrameId, result.CoordinateSpaceId, 1),
            GrayHistogramOutput result => (result.FrameId, result.CoordinateSpaceId, 1),
            ImageCropOutput result => (result.FrameId, result.SourceCoordinateSpaceId, 1),
            ImageTransformOutput result => (result.FrameId, result.Image.CoordinateSpaceId, 1),
            GrayNormalizeOutput result => (result.FrameId, result.Image.CoordinateSpaceId, 1),
            OtsuThresholdOutput result => (result.FrameId, result.Image.CoordinateSpaceId, 1),
            BlobOutput result => (result.FrameId, result.CoordinateSpaceId, result.Items.Count),
            CaliperLineOutput result => (result.FrameId, result.CoordinateSpaceId, result.Line == null ? 0 : 1),
            CaliperEdgePairOutput result => (result.FrameId, result.CoordinateSpaceId, result.Statistics == null ? 0 : 1),
            CaliperCircleOutput result => (result.FrameId, result.CoordinateSpaceId, result.Circle == null ? 0 : 1),
            _ => throw new InvalidOperationException("后端返回了未约定的输出。")
        };
        if (identity.Item1 != input.FrameId || identity.Item2 != input.CoordinateSpaceId)
        {
            throw new InvalidOperationException("后端返回了其他帧或坐标空间的结果。");
        }

        if (output.Value is AiClassificationOutput classified)
        {
            if (operation is not AiClassificationOperation classification
                || classified.Predictions.Count > classification.MaximumResults
                || classified.Predictions.Any(prediction => prediction.Score < classification.MinimumScore)
                || classified.Predictions.Select(prediction => prediction.ClassId).Distinct(StringComparer.Ordinal).Count() != classified.Predictions.Count)
            {
                throw new InvalidOperationException("分类结果数量、最低分数或类别ID唯一性与本次请求不符，不能截断或修补后发布。");
            }
            for (var index = 1; index < classified.Predictions.Count; index++)
            {
                if (classified.Predictions[index].Score > classified.Predictions[index - 1].Score)
                {
                    throw new InvalidOperationException("分类结果必须按分数降序发布，不重排后端坏结果来掩盖契约错误。");
                }
            }
        }

        if (output.Value is QrCodeReadOutput qr
            && (operation is not QrCodeReadOperation read || output.NoMatch != (qr.State != QrCodeReadState.Decoded)
                || qr.Corners.Any(point => point.X < read.X - 1e-3 || point.Y < read.Y - 1e-3
                    || point.X > (long)read.X + read.Width - 1 + 1e-3 || point.Y > (long)read.Y + read.Height - 1 + 1e-3)))
        {
            throw new InvalidOperationException("QR状态或原图四角超出本次搜索窗口，不能发布错分支/错位置结果。");
        }

        if (output.Value is QrCodeReadManyOutput manyQr
            && (operation is not QrCodeReadManyOperation manyRead || manyQr.DetectedCount > manyRead.MaximumCandidates
                || output.NoMatch != (manyQr.DecodedCount == 0)
                || manyQr.Candidates.SelectMany(candidate => candidate.Corners).Any(point => point.X < manyRead.X - 1e-3
                    || point.Y < manyRead.Y - 1e-3 || point.X > (long)manyRead.X + manyRead.Width - 1 + 1e-3
                    || point.Y > (long)manyRead.Y + manyRead.Height - 1 + 1e-3)))
        {
            throw new InvalidOperationException("多QR来源、候选预算、完成状态或四角与本次搜索窗口不符。");
        }

        var binaryImage = output.Value switch
        {
            BinaryImageOutput binary => binary.Image,
            ImageDifferenceOutput differenceImage => differenceImage.Image,
            OtsuThresholdOutput otsu => otsu.Image,
            _ => null
        };
        if (binaryImage != null && (binaryImage.Width != input.Width || binaryImage.Height != input.Height))
        {
            throw new InvalidOperationException("全图二值处理不能改变图像尺寸。");
        }

        if (output.Value is ImageDifferenceOutput difference
            && (operation is not AlignedImageDifferenceOperation comparison || output.NoMatch
                || difference.ReferenceFrameId != comparison.Reference.FrameId || difference.Direction != comparison.Direction
                || difference.MinimumDifference != comparison.MinimumDifference || difference.MaximumDifference != comparison.MaximumDifference))
        {
            throw new InvalidOperationException("双图差分的参考来源、方向、阈值或成功状态与本次请求不符。");
        }

        var statisticsRegion = operation switch
        {
            GrayStatisticsOperation statisticsRequest => statisticsRequest.Region,
            GrayHistogramOperation histogramRegionRequest => histogramRegionRequest.Region,
            _ => null
        };
        var expectedCount = statisticsRegion == null ? (long)input.Width * input.Height
            : statisticsRegion.Runs(input.Width, input.Height).Sum(run => (long)run.End - run.Start + 1);
        if (output.Value is GrayHistogramOutput histogram
            && (operation is not GrayHistogramOperation histogramRequest
                || histogram.Format != input.Format || histogram.BinWidth != histogramRequest.BinWidth
                || histogram.PixelCount != expectedCount))
        {
            throw new InvalidOperationException("直方图格式、箱宽或计数与本次输入/参与域不符。");
        }
        if (output.Value is GrayStatisticsOutput statistics &&
            (statistics.Format != input.Format || statistics.PixelCount != expectedCount ||
             statistics.Maximum > (input.Format == Vision.Core.Imaging.PixelFormat.Mono8 ? byte.MaxValue : ushort.MaxValue)))
        {
            throw new InvalidOperationException("灰度统计像素数或灰度范围与本次参与域不符。");
        }

        if (output.Value is ImageCropOutput cropped
            && (operation is not ImageCropOperation crop
                || cropped.SourceX != crop.X || cropped.SourceY != crop.Y
                || cropped.Image.Width != crop.Width || cropped.Image.Height != crop.Height
                || cropped.Image.Format != input.Format))
        {
            throw new InvalidOperationException("裁剪输出尺寸、格式或来源映射与本次请求不符。");
        }

        if (output.Value is GrayNormalizeOutput normalized
            && (operation is not GrayNormalizeOperation || normalized.Image.Width != input.Width
                || normalized.Image.Height != input.Height || normalized.Image.Format != input.Format))
        {
            throw new InvalidOperationException("灰度归一化输出格式或尺寸与本次请求不符。");
        }

        if (output.Value is ImageTransformOutput transformed)
        {
            var expectedFormat = operation switch
            {
                ImageConvertOperation conversion => conversion.OutputFormat,
                ColorChannelExtractOperation => Vision.Core.Imaging.PixelFormat.Mono8,
                GrayLinearOperation linear => linear.OutputFormat,
                GrayGammaOperation gamma => gamma.Image.Format,
                ImageFilterOperation filter => filter.Image.Format,
                _ => throw new InvalidOperationException("操作没有约定此图像变换结果。")
            };
            if (transformed.Image.Width != input.Width || transformed.Image.Height != input.Height
                || transformed.Image.Format != expectedFormat)
            {
                throw new InvalidOperationException("图像变换输出格式或尺寸与本次请求不符。");
            }
        }

        if (output.Value is OtsuThresholdOutput selected
            && (operation is not OtsuThresholdOperation
                || selected.Threshold > (input.Format == Vision.Core.Imaging.PixelFormat.Mono8 ? byte.MaxValue : ushort.MaxValue)))
        {
            throw new InvalidOperationException("Otsu返回的阈值不属于本次输入位深。");
        }

        if (output.Value is BlobOutput blobs)
        {
            if (operation is not BlobOperation request || blobs.Items.Count > request.MaximumResults
                || blobs.Items.Any(item => item.Bounds.Right > input.Width || item.Bounds.Bottom > input.Height
                    || item.Center.X < 0 || item.Center.Y < 0 || item.Center.X >= input.Width || item.Center.Y >= input.Height))
            {
                throw new InvalidOperationException("区域结果数量或几何超出本次输入和请求范围。");
            }
        }

        if (output.Value is CaliperLineOutput calipers)
        {
            if (operation is not CaliperLineOperation request || calipers.Samples.Count != request.CaliperCount
                || calipers.Line is { } fit && (fit.PointCount < request.MinimumPoints
                    || fit.MaximumResidualPixels > request.MaximumResidualPixels + 1e-9)
                || calipers.Samples.Any(sample => sample.Edge is { } point
                    && (point.X < 0 || point.Y < 0 || point.X >= input.Width || point.Y >= input.Height)))
            {
                throw new InvalidOperationException("卡尺样本数量、参与点、残差或坐标超出本次请求。");
            }
        }

        if (output.Value is CaliperEdgePairOutput pairs)
        {
            if (operation is not CaliperEdgePairOperation request || pairs.Samples.Count != request.CaliperCount
                || (pairs.Statistics != null) != (pairs.Samples.Count(sample => sample.WidthPixels != null) >= request.MinimumPairs)
                || pairs.Samples.Any(sample => sample.WidthPixels is { } width
                    && (width < request.MinimumWidthPixels || width > request.MaximumWidthPixels
                        || !Inside(sample.FirstEdge!.Value) || !Inside(sample.SecondEdge!.Value))))
            {
                throw new InvalidOperationException("边缘对数量、宽度、来源坐标或统计状态与本次请求不符。");
            }

            bool Inside(Vision.Core.Geometry.Point2D point) => point.X >= 0 && point.Y >= 0 && point.X < input.Width && point.Y < input.Height;
        }

        if (output.Value is CaliperCircleOutput circle)
        {
            if (operation is not CaliperCircleOperation request || circle.Samples.Count != request.CaliperCount
                || circle.Circle is { } fit && (fit.PointCount < request.MinimumPoints
                    || fit.MaximumRadialResidualPixels > request.MaximumRadialResidualPixels + 1e-9
                    || fit.CoverageDegrees < request.MinimumCoverageDegrees)
                || circle.Samples.Any(sample => sample.Edge is { } point
                    && (point.X < 0 || point.Y < 0 || point.X >= input.Width || point.Y >= input.Height)))
            {
                throw new InvalidOperationException("圆卡尺数量、参与点、覆盖角、径向残差或坐标与本次请求不符。");
            }
        }

        if (output.NoMatch != (identity.Item3 == 0))
        {
            throw new InvalidOperationException("未找到状态与本次结果数量矛盾。");
        }
    }

}
