#nullable enable

namespace Vision.Core.Capabilities;

/// <summary>
/// 操作/功能位快照，不保证所有格式、度量、参数组合或运行环境；具体请求以ValidateAsync为准。
/// </summary>
public sealed record VisionOperationCapability
{
    /// <summary>
    /// 操作的稳定标识。
    /// </summary>
    public string OperationId
    {
        get;
    }

    /// <summary>
    /// 该操作所属的执行域。
    /// </summary>
    public VisionExecutionDomain Domain
    {
        get;
    }

    /// <summary>
    /// 该操作明确声明的可选能力。
    /// </summary>
    public VisionOperationFeatures Features
    {
        get;
    }

    /// <summary>
    /// 创建操作能力描述，拒绝未知执行域和无效能力位。
    /// </summary>
    public VisionOperationCapability(
        string operationId,
        VisionExecutionDomain domain,
        VisionOperationFeatures features)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (!Enum.IsDefined(domain))
        {
            throw new ArgumentOutOfRangeException(nameof(domain));
        }

        const VisionOperationFeatures all = VisionOperationFeatures.Rotation | VisionOperationFeatures.UniformScale |
            VisionOperationFeatures.TemplateMask | VisionOperationFeatures.SearchMask |
            VisionOperationFeatures.MultipleMatches | VisionOperationFeatures.IgnorePolarity | VisionOperationFeatures.NativeAutomaticSearch
            | VisionOperationFeatures.ProcessingRegion;
        if ((features & ~all) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(features));
        }

        OperationId = operationId;
        Domain = domain;
        Features = features;
    }
}
