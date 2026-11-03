#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Imaging;
using Vision.Core.Models;

namespace Vision.Core.Engine;

/// <summary>
/// 强类型执行请求的公共语义；不含后端切换、厂商对象或任意选项字典。
/// </summary>
public abstract class VisionOperation
{
    /// <summary>
    /// 操作的稳定标识。
    /// </summary>
    public abstract string OperationId
    {
        get;
    }

    /// <summary>
    /// 该操作所属的执行域。
    /// </summary>
    public abstract VisionExecutionDomain Domain
    {
        get;
    }

    /// <summary>
    /// 本次请求必须满足的可选能力。
    /// </summary>
    public abstract VisionOperationFeatures RequiredFeatures
    {
        get;
    }

    /// <summary>
    /// 本次操作的可选图像输入。数值/几何请求可以没有图像，不能构造假帧。
    /// 图像请求由 VisionImageOperation 强制提供非空快照。
    /// </summary>
    public virtual VisionFrame? Image => null;

    /// <summary>
    /// 当前已准备的模型引用；无模型操作时为空。
    /// </summary>
    public VisionModelReference? Model
    {
        get;
    }

    protected VisionOperation(VisionModelReference? model = null)
    {
        Model = model;
    }

    /// <summary>
    /// 不访问SDK的前置检查；实现还须检查模型注册状态、格式/方法支持和运行环境。
    /// </summary>
    public IReadOnlyList<VisionIssue> Validate(VisionEngineDescriptor descriptor, VisionContext context)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);
        List<VisionIssue> issues = [];
        if (context.EngineInstanceId != descriptor.EngineInstanceId
            || (Model != null
            && Model.Context != context))
        {
            issues.Add(new("ContextMismatch", "上下文或模型不属于当前引擎/工具实例。"));
        }

        var selectedBackend = Domain == VisionExecutionDomain.Vision
            ? descriptor.Selection.VisionBackendId : descriptor.Selection.AiBackendId;
        if (Model != null && !string.Equals(Model.BackendId, selectedBackend, StringComparison.Ordinal))
        {
            issues.Add(new("BackendMismatch", "模型不属于已选实现；不允许跨后端复用或回退。"));
        }

        var capability = descriptor.Operations.SingleOrDefault(operation => operation.OperationId == OperationId);
        if (capability == null
            || capability.Domain != Domain
            || (RequiredFeatures & ~capability.Features) != 0)
        {
            issues.Add(new("UnsupportedCapability", "所选实现不支持此操作或所需功能组合。"));
        }

        ValidateParameters(issues);
        return issues.AsReadOnly();
    }

    protected abstract void ValidateParameters(List<VisionIssue> issues);
}

/// <summary>
/// 结果类型由操作定义，调用者不能另行指定不相容的返回类型。
/// </summary>
public abstract class VisionOperation<TResult> : VisionOperation where TResult : class
{
    protected VisionOperation(VisionModelReference? model = null)
        : base(model)
    {
    }
}
