using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;

namespace Vision.Tools.Execution;

/// <summary>
/// 已选厂商模块的内部协作协议，不是第二个业务接口。
/// 无模型操作传入 null；准备返回独立候选资源，由引擎登记并接管所有权。
/// </summary>
internal interface IVisionBackend : IDisposable
{
    string Id
    {
        get;
    }
    VisionExecutionDomain Domain
    {
        get;
    }
    IReadOnlyList<VisionOperationCapability> Capabilities
    {
        get;
    }

    IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model);

    ValueTask<IPreparedVisionModel> PrepareAsync(
        VisionModelDefinition definition,
        CancellationToken cancellationToken);

    ValueTask<BackendOutput> ExecuteAsync(
        IPreparedVisionModel? model,
        VisionOperation operation,
        CancellationToken cancellationToken);
}
