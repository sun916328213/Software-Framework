using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Tools.Execution;

/// <summary>模型准备事务：先登记候选所有权，成功后原子提交，失败保留当前模型。</summary>
internal sealed partial class VisionEngine
{
    public async ValueTask<VisionModelReference> PrepareModelAsync(
        VisionContext context,
        VisionModelDefinition definition,
        long revision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        using var lease = await EnterAsync(context, cancellationToken).ConfigureAwait(false);
        var state = lease.State;
        if (state.CleanupFault != null)
        {
            throw new InvalidOperationException("上下文有待清理资源，必须关闭后重建。", state.CleanupFault);
        }
        // 类型与种类必须共同匹配；未知定义不能因枚举值相同而进入某个 SDK。
        var backend = definition switch
        {
            TemplateModelDefinition { Kind: VisionModelKind.Template } => _vision,
            AiClassificationModelDefinition { Kind: VisionModelKind.AiClassification } => _ai
                ?? throw new NotSupportedException("未启用AI实现。"),
            _ => throw new NotSupportedException("尚未接通此模型定义，不推断执行域或回退实现。")
        };
        IPreparedVisionModel? candidate = null;
        try
        {
            lease.Token.ThrowIfCancellationRequested();
            candidate = await backend.PrepareAsync(definition, lease.Token).ConfigureAwait(false);
            if (candidate == null)
            {
                throw new InvalidOperationException("后端必须返回新模型资源。");
            }

            lock (_gate)
            {
                if (!_ownedModels.Add(candidate))
                {
                    candidate = null; // 已属于任一上下文，不是本次候选，不能误释放别人的资源。
                    throw new InvalidOperationException("后端必须返回独立的新模型资源。");
                }
                state.Models.Add(candidate); // 候选也登记所有权，清理失败时不遗失。
            }
            var reference = new VisionModelReference(context, Guid.NewGuid(), backend.Id, definition, revision);
            var previous = state.Active;
            lock (_gate)
            {
                ThrowIfClosing(state, lease.Token);
                state.Active = new(reference, candidate);
                candidate = null; // 从候选事务转移到上下文，关闭链负责释放。
            }
            if (previous != null)
            {
                try
                {
                    previous.Resource.Dispose();
                    ForgetModel(state, previous.Resource);
                }
                catch (Exception error)
                {
                    // 新模型已原子提交，不倒退成“失败但已替换”。保留旧资源供关闭重试，校验明确报告故障。
                    state.CleanupFault = error;
                }
            }
            return reference;
        }
        catch (Exception original)
        {
            if (candidate != null)
            {
                try
                {
                    candidate.Dispose();
                    ForgetModel(state, candidate);
                }
                catch (Exception cleanup)
                {
                    state.CleanupFault = cleanup;
                    throw new AggregateException("准备失败且候选清理失败。", original, cleanup);
                }
            }
            throw;
        }
    }

}
