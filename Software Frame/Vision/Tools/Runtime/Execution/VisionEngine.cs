using System.Runtime.CompilerServices;
using Vision.Tools.Common;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Tools.Execution;

/// <summary>唯一门面的真实上下文/模型所有权；锁仅保护状态，不包围SDK、取消回调或异步等待。</summary>
internal sealed partial class VisionEngine : IVisionEngine
{
    private readonly object _gate = new();
    // 只登记实际发布的结果对象；弱键不把每帧结果无限保留，也不持有额外原生资源。
    private readonly ConditionalWeakTable<TemplateMatchOutput, PublishedLocation> _publishedLocations = new();
    private readonly Dictionary<Guid, ContextState> _contexts = [];
    private readonly HashSet<IPreparedVisionModel> _ownedModels = new(ReferenceEqualityComparer.Instance);
    private readonly IVisionBackend _vision;
    private readonly IVisionBackend? _ai;
    private readonly HashSet<IVisionBackend> _releasedBackends = new(ReferenceEqualityComparer.Instance);
    private bool _closing;
    private TaskCompletionSource? _dispose;
    public VisionEngineDescriptor Descriptor
    {
        get;
    }

    internal VisionEngine(VisionEngineSelection selection, IVisionBackend vision, IVisionBackend? ai)
    {
        _vision = vision;
        _ai = ai;
        Descriptor = new(Guid.NewGuid(), selection,
            CommonVisionOperations.Capabilities.Concat(vision.Capabilities).Concat(ai?.Capabilities ?? []));
    }

    public ValueTask<VisionContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            var context = new VisionContext(Descriptor.EngineInstanceId, Guid.NewGuid());
            _contexts.Add(context.ContextId, new(context));
            return ValueTask.FromResult(context);
        }
    }

}
