using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.Execution;

internal sealed partial class VisionEngine
{
    /// <summary>调用前与发布前均核验依赖，避免另一上下文替换模型后继续发布旧定位派生结果。</summary>
    private VisionIssue? CheckLocation(VisionOperation operation)
    {
        (TemplateMatchOutput? location, VisionModelReference? model) = operation switch
        {
            BlobOperation blob => (blob.RegionBinding?.Location, blob.RegionBinding?.LocationModel),
            CaliperLineOperation line => (line.LocationBinding?.Location, line.LocationBinding?.Model),
            CaliperEdgePairOperation pair => (pair.LocationBinding?.Location, pair.LocationBinding?.Model),
            CaliperCircleOperation circle => (circle.LocationBinding?.Location, circle.LocationBinding?.Model),
            _ => (null, null)
        };
        if (location == null || model == null)
        {
            return null;
        }

        lock (_gate)
        {
            if (!_publishedLocations.TryGetValue(location, out var original) || !ReferenceEquals(original.Model, model))
            {
                return new("LocationNotPublished", "定位不是本引擎为该模型实际发布的原结果，不凭重造GUID或数值授权复用。");
            }

            if (!original.Image.TryGetTarget(out var input) || !ReferenceEquals(input, operation.Image))
            {
                return new("LocationFrameMismatch", "跟随必须使用实际定位输入快照，重造相同GUID不能证明两个图像分支等价。");
            }

            if (model.Context.EngineInstanceId != Descriptor.EngineInstanceId
                || !_contexts.TryGetValue(model.Context.ContextId, out var owner) || owner.Closing
                || !ReferenceEquals(owner.Active?.Reference, model))
            {
                return new("LocationModelNotCurrent", "定位源模型已替换、关闭或不再属于当前修订，必须重新定位。");
            }
        }

        return null;
    }

    /// <summary>仅在已校验结果真正发布时登记模型来源，调用方拼装的DTO不能进入此账本。</summary>
    private void RememberLocation(VisionOperation operation, BackendOutput output)
    {
        if (operation is not TemplateMatchOperation match || output.Value is not TemplateMatchOutput result)
        {
            return;
        }

        if (_publishedLocations.TryGetValue(result, out var existing))
        {
            if (!ReferenceEquals(existing.Model, match.Model)
                || !existing.Image.TryGetTarget(out var input) || !ReferenceEquals(input, match.Image))
            {
                throw new InvalidOperationException("后端不能把已发布的定位对象重新归属到其他模型或输入帧。");
            }
        }
        else
        {
            _publishedLocations.Add(result, new PublishedLocation(match.Model!, new WeakReference<VisionFrame>(match.Image)));
        }
    }

    // 不额外强持有完整搜索图；需要跟随时请求仍须持有原不可变图像对象。
    private sealed record PublishedLocation(VisionModelReference Model, WeakReference<VisionFrame> Image);
}
