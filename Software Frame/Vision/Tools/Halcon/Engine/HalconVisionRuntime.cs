using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using HalconDotNet;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.Halcon.Engine;

/// <summary>HALCON23.11直接视觉SDK适配，模型/处理的失败资源与临时对象保留到关闭重试。</summary>
internal sealed partial class HalconVisionRuntime : IHalconVisionRuntime
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _pending = [];
    private Exception? _cleanupFailure;
    public Exception? CleanupFailure
    {
        get
        {
            lock (_gate)
            {
                return _cleanupFailure;
            }
        }
    }

    internal HalconVisionRuntime()
    {
        HOperatorSet.GetSystem("version", out var version);
        version.Dispose();
    }

    public IHalconShapeModel Prepare(TemplateModelDefinition definition)
    {
        var model = new NativeModel(this, definition);
        try
        {
            Use(resources =>
            {
                model.Open(resources);
                return true;
            });
            return model;
        }
        catch (Exception original)
        {
            try
            {
                model.Dispose();
            }
            catch (Exception cleanup)
            {
                Retain(model, cleanup);
                throw new AggregateException("HALCON形状建模及清理失败。", original, cleanup);
            }
            throw;
        }
    }

    private T Use<T>(Func<List<IDisposable>, T> action)
    {
        if (CleanupFailure != null)
        {
            throw new InvalidOperationException("HALCON有待清理资源，请关闭引擎。");
        }

        List<IDisposable> resources = [];
        List<Exception> errors = [];
        T result = default!;
        try
        {
            result = action(resources);
        }
        catch (Exception original)
        {
            errors.Add(original);
        }
        foreach (var resource in resources.AsEnumerable().Reverse())
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception cleanup)
            {
                Retain(resource, cleanup);
                errors.Add(cleanup);
            }
        }
        if (errors.Count == 1)
        {
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }

        if (errors.Count > 1)
        {
            throw new AggregateException("HALCON形状操作及清理失败。", errors);
        }

        return result;
    }

    private void Retain(IDisposable resource, Exception failure)
    {
        lock (_gate)
        {
            if (!_pending.Any(item => ReferenceEquals(item, resource)))
            {
                _pending.Add(resource);
            }

            _cleanupFailure = failure;
        }
    }

    public void Dispose()
    {
        IDisposable[] pending;
        lock (_gate)
        {
            pending = _pending.ToArray();
        }

        List<Exception> errors = [];
        foreach (var resource in pending)
        {
            try
            {
                resource.Dispose();
                lock (_gate)
                {
                    _pending.RemoveAll(item => ReferenceEquals(item, resource));
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }
        if (errors.Count != 0)
        {
            throw new AggregateException("HALCON待清理形状资源仍未释放。", errors);
        }

        lock (_gate)
        {
            _cleanupFailure = null;
        }
    }

    private static HObject Image(VisionFrame frame, List<IDisposable> resources)
    {
        if (frame.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
        {
            throw new NotSupportedException("只支持Mono8/Mono16。");
        }

        var rowBytes = checked(frame.Width * (frame.Format == PixelFormat.Mono16 ? 2 : 1));
        var bytes = new byte[checked(rowBytes * frame.Height)];
        for (var row = 0; row < frame.Height; row++)
        {
            frame.Pixels.Slice(row * frame.Stride, rowBytes).CopyTo(bytes.AsSpan(row * rowBytes));
        }

        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            HOperatorSet.GenImage1(out var image, frame.Format == PixelFormat.Mono16 ? "uint2" : "byte", frame.Width, frame.Height, pin.AddrOfPinnedObject());
            resources.Add(image);
            return image;
        }
        finally
        {
            pin.Free();
        } // GenImage1已复制到HALCON自有存储。
    }

    private sealed class NativeModel(HalconVisionRuntime runtime, TemplateModelDefinition definition) : IHalconShapeModel
    {
        private HTuple? _model;
        private bool _cleared;

        internal void Open(List<IDisposable> resources)
        {
            var image = Image(definition.Template, resources);
            HObject domain;
            if (definition.Mask != null)
            {
                var mask = definition.Mask;
                var maskFrame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), mask.Width, mask.Height, PixelFormat.Mono8, mask.Pixels);
                var maskImage = Image(maskFrame, resources);
                HOperatorSet.Threshold(maskImage, out domain, 1, 255);
                resources.Add(domain);
                HOperatorSet.ReduceDomain(image, domain, out var reduced);
                resources.Add(reduced);
                image = reduced;
            }
            else
            {
                HOperatorSet.GetDomain(image, out domain);
                resources.Add(domain);
            }
            HOperatorSet.AreaCenter(domain, out var area, out var row, out var column);
            resources.Add(area);
            resources.Add(row);
            resources.Add(column);
            if (area.Length != 1 || area.D <= 0)
            {
                throw new ArgumentException("模板参与域必须非空。");
            }

            if (!HalconShapeCoordinates.TryNativeRange(definition.PreparedRange, definition.PreparedRange, out var angles))
            {
                throw new ArgumentException("无效形状模型角度范围。");
            }

            HOperatorSet.CreateScaledShapeModel(image, "auto", angles.Start, angles.Extent, "auto",
                definition.PreparedRange.ScaleMinimum, definition.PreparedRange.ScaleMaximum, "auto", "auto",
                definition.IgnorePolarity ? "ignore_local_polarity" : "use_polarity", "auto", "auto", out _model);
            // SDK偏移以建模域质心为原点，不能误用矩形中心，掩膜带孔/不对称时尤甚。
            HOperatorSet.SetShapeModelOrigin(_model, definition.Origin.Y - row.D, definition.Origin.X - column.D);
        }

        public IReadOnlyList<HalconShapeObservation> Run(TemplateMatchOperation operation)
        {
            ObjectDisposedException.ThrowIf(_cleared || _model == null, this);
            return runtime.Use<IReadOnlyList<HalconShapeObservation>>(resources =>
            {
                if (!HalconShapeCoordinates.TryNativeRange(definition.PreparedRange, operation.SearchRange, out var angles))
                {
                    throw new NotSupportedException("不能静默裁剪跨原生模型边界的搜索范围。");
                }

                var image = Image(operation.Image, resources);
                HTuple? row = null, column = null, angle = null, scale = null, score = null;
                try
                {
                    HOperatorSet.FindScaledShapeModel(image, _model!, angles.Start, angles.Extent,
                        operation.SearchRange.ScaleMinimum, operation.SearchRange.ScaleMaximum,
                        operation.MinimumScore, operation.MaximumMatches, operation.MaximumOverlap,
                        "least_squares", 0, 0.9, out row, out column, out angle, out scale, out score);
                    var rows = row.DArr;
                    var columns = column.DArr;
                    var anglesFound = angle.DArr;
                    var scales = scale.DArr;
                    var scores = score.DArr;
                    if (rows.Length != scores.Length || columns.Length != scores.Length || anglesFound.Length != scores.Length || scales.Length != scores.Length)
                    {
                        throw new InvalidDataException("HALCON形状结果元组长度不一致。");
                    }

                    return scores.Select((value, index) => new HalconShapeObservation(rows[index], columns[index], anglesFound[index], scales[index], value)).ToArray();
                }
                finally
                {
                    if (row != null)
                    {
                        resources.Add(row);
                    }

                    if (column != null)
                    {
                        resources.Add(column);
                    }

                    if (angle != null)
                    {
                        resources.Add(angle);
                    }

                    if (scale != null)
                    {
                        resources.Add(scale);
                    }

                    if (score != null)
                    {
                        resources.Add(score);
                    }
                }
            });
        }

        public void Dispose()
        {
            if (_model == null)
            {
                return;
            }

            if (!_cleared)
            {
                if (_model.Length > 0)
                {
                    HOperatorSet.ClearShapeModel(_model);
                }

                _cleared = true;
            }
            _model.Dispose();
            _model = null;
        }
    }
}
