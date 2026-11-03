using System.Security.Cryptography;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.AI.Halcon.Engine;

/// <summary>新统一入口的HALCON分类实现；显式CPU/原尺寸原范围输入，不借用旧Provider。</summary>
internal sealed class HalconClassificationBackend : IVisionBackend
{
    private readonly string _deviceId;
    private readonly IHalconClassificationRuntime _runtime;
    private bool _disposed;
    public string Id => "halcon";
    public VisionExecutionDomain Domain => VisionExecutionDomain.Ai;
    public IReadOnlyList<VisionOperationCapability> Capabilities
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        new VisionOperationCapability(AiClassificationOperation.Id, VisionExecutionDomain.Ai, VisionOperationFeatures.None)
    });

    internal HalconClassificationBackend(string deviceId)
        : this(deviceId, CreateRuntime(deviceId)) { }

    internal HalconClassificationBackend(string deviceId, IHalconClassificationRuntime runtime)
    {
        EnsureDevice(deviceId);
        _deviceId = deviceId;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    private static IHalconClassificationRuntime CreateRuntime(string deviceId)
    {
        EnsureDevice(deviceId); // 在任何SDK探测前拒绝未实现设备。
        return new HalconClassificationRuntime();
    }

    private static void EnsureDevice(string deviceId)
    {
        if (deviceId != "cpu")
        {
            throw new NotSupportedException("HALCON分类首批仅支持显式cpu，不回退或忽略设备。");
        }
    }

    public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        List<VisionIssue> issues = [];
        if (_runtime.CleanupFailure != null)
        {
            issues.Add(new("ResourceCleanupFailed", "HALCON候选有待清理资源，必须关闭引擎后重建。"));
        }

        if (operation is not AiClassificationOperation classification)
        {
            return [new("UnsupportedOperation", "HALCON AI首批仅支持单标签分类。")];
        }

        if (classification.Image.Format != PixelFormat.Mono8)
        {
            issues.Add(new("UnsupportedPixelFormat", "分类首批只接收Mono8，不隐式转换颜色/位深。"));
        }

        if (model is not IHalconClassificationModel prepared)
        {
            issues.Add(new("UnsupportedModel", "需要已准备的HALCON分类模型。"));
        }
        else if (classification.Image.Width != prepared.Width || classification.Image.Height != prepared.Height)
        {
            issues.Add(new("UnsupportedPreprocessing", "图像必须与模型原尺寸一致；未配置缩放、裁剪或归一化。"));
        }

        return issues.AsReadOnly();
    }

    public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken cancellationToken)
        => new(Task.Run<IPreparedVisionModel>(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (definition is not AiClassificationModelDefinition classification)
            {
                throw new NotSupportedException("只准备AI单标签分类模型。");
            }

            var path = Path.GetFullPath(classification.Location);
            // Windows文件读租约贯穿指纹校验和SDK加载，禁止正常文件写入/删除竞争。
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(source));
            if (!hash.Equals(classification.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("模型内容SHA256与配方不符，未调用SDK加载。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            // 返回资源后由共享引擎观察取消并清理候选，不能在此丢弃迟到资源。
            return _runtime.Load(path, _deviceId);
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
            var classification = (AiClassificationOperation)operation;
            var raw = ((IHalconClassificationModel)model!).Run(classification.Image)?.ToArray()
                ?? throw new InvalidDataException("HALCON原始分类结果不能为空。");
            cancellationToken.ThrowIfCancellationRequested();
            // 原始类别身份必须唯一；先完整核验，不能让阈值或结果数量隐藏坏数据。
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (raw.Any(item => item == null || !ids.Add(item.ClassId)))
            {
                throw new InvalidDataException("HALCON原始分类结果包含空项或重复类别ID，不能筛选后发布。");
            }

            var predictions = raw.Where(item => item.Score >= classification.MinimumScore)
                .OrderByDescending(item => item.Score).Take(classification.MaximumResults).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            return new BackendOutput(new AiClassificationOutput(classification.Image.FrameId,
                classification.Image.CoordinateSpaceId, predictions), predictions.Length == 0);
        }));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _runtime.Dispose(); // 失败不标已释放，交由共享引擎重试。
        _disposed = true;
    }
}
