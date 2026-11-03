using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using HalconDotNet;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.AI.Halcon.Engine;

/// <summary>直接SDK适配，不依赖旧Provider/Session/结果转换。只在选中该AI模块后构造。</summary>
internal sealed class HalconClassificationRuntime : IHalconClassificationRuntime
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

    internal HalconClassificationRuntime()
    {
        HOperatorSet.GetSystem("version", out var version);
        version.Dispose(); // 真Runtime探测，错误直接上报，不返回假可用状态。
    }

    public IHalconClassificationModel Load(string path, string deviceId)
    {
        var model = new NativeModel(this);
        try
        {
            Use(resources =>
            {
                model.Open(path, deviceId, resources);
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
                throw new AggregateException("HALCON模型初始化及清理失败。", original, cleanup);
            }
            throw;
        }
    }

    // 无论主操作是否成功，均尝试清理所有临时对象；失败对象保留给引擎关闭重试。
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
            throw new AggregateException("HALCON操作及资源清理失败。", errors);
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
            throw new AggregateException("HALCON待清理资源仍未释放。", errors);
        }

        lock (_gate)
        {
            _cleanupFailure = null;
        }
    }

    private sealed class NativeModel(HalconClassificationRuntime runtime) : IHalconClassificationModel
    {
        private HTuple? _model;
        private bool _cleared;
        public int Width
        {
            get; private set;
        }
        public int Height
        {
            get; private set;
        }

        internal void Open(string path, string deviceId, List<IDisposable> resources)
        {
            if (deviceId != "cpu")
            {
                throw new NotSupportedException("仅支持显式cpu。");
            }

            HOperatorSet.ReadDlModel(path, out _model);
            HOperatorSet.SetDlModelParam(_model, "runtime", "cpu");
            if (Read("type", resources).S != "classification")
            {
                throw new NotSupportedException("模型必须为单标签classification。");
            }

            Width = Read("image_width", resources).I;
            Height = Read("image_height", resources).I;
            if (Width <= 0 || Height <= 0 || Read("image_num_channels", resources).I != 1 ||
                Read("image_range_min", resources).D != 0 || Read("image_range_max", resources).D != 255)
            {
                throw new NotSupportedException("首批仅支持单通道、0..255原范围模型；其它输入需要显式预处理配方，不能自动猜测。");
            }

            HOperatorSet.SetDlModelParam(_model, "batch_size", 1);
        }

        private HTuple Read(string name, List<IDisposable> resources)
        {
            HOperatorSet.GetDlModelParam(_model!, name, out var value);
            resources.Add(value);
            return value;
        }

        public IReadOnlyList<AiClassPrediction> Run(VisionFrame image)
        {
            ObjectDisposedException.ThrowIf(_cleared || _model == null, this);
            if (image.Format != PixelFormat.Mono8 || image.Width != Width || image.Height != Height)
            {
                throw new NotSupportedException("图像必须为模型原尺寸Mono8。");
            }

            return runtime.Use<IReadOnlyList<AiClassPrediction>>(resources =>
            {
                var bytes = new byte[checked(image.Width * image.Height)];
                for (var row = 0; row < image.Height; row++)
                {
                    image.Pixels.Slice(row * image.Stride, image.Width).CopyTo(bytes.AsSpan(row * image.Width));
                }

                var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                HObject input;
                try
                {
                    HOperatorSet.GenImage1(out input, "byte", image.Width, image.Height, pin.AddrOfPinnedObject());
                }
                finally
                {
                    pin.Free();
                } // GenImage1复制输入；之后不借用托管指针。
                resources.Add(input);
                HOperatorSet.ConvertImageType(input, out var real, "real");
                resources.Add(real);
                HOperatorSet.CreateDict(out var sample);
                resources.Add(sample);
                HOperatorSet.SetDictObject(real, sample, "image");
                var outputs = new HTuple();
                resources.Add(outputs);
                HOperatorSet.ApplyDlModel(_model!, sample, outputs, out var result);
                resources.Add(result);
                HOperatorSet.GetDictTuple(result, "classification_class_ids", out var ids);
                resources.Add(ids);
                HOperatorSet.GetDictTuple(result, "classification_confidences", out var confidences);
                resources.Add(confidences);
                var labels = ids.LArr;
                var scores = confidences.DArr;
                if (labels.Length != scores.Length || labels.Distinct().Count() != labels.Length || labels.Length == 0)
                {
                    throw new InvalidDataException("分类结果ID/分数数量不一致、为空或包含重复ID。");
                }

                return labels.Select((id, index) => new AiClassPrediction(id.ToString(CultureInfo.InvariantCulture), scores[index])).ToArray();
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
                    HOperatorSet.ClearDlModel(_model);
                }

                _cleared = true; // 原生释放成功才撤销，后续仅重试托管tuple释放。
            }
            _model.Dispose();
            _model = null;
        }
    }
}
