#nullable enable
using Vision.Core.Engine;

namespace Vision.Core.Capabilities;

/// <summary>
/// 引擎装配快照。复制能力集合，拒绝身份冲突和未启用AI却声明AI能力。
/// </summary>
public sealed class VisionEngineDescriptor
{
    /// <summary>
    /// 所属引擎实例的唯一标识。
    /// </summary>
    public Guid EngineInstanceId
    {
        get;
    }

    /// <summary>
    /// 装配时冻结的实现和设备选择。
    /// </summary>
    public VisionEngineSelection Selection
    {
        get;
    }

    /// <summary>
    /// 已选实现声明的操作能力只读快照。
    /// </summary>
    public IReadOnlyList<VisionOperationCapability> Operations
    {
        get;
    }

    /// <summary>
    /// 创建引擎能力快照，拒绝重复操作或未启用的执行域。
    /// </summary>
    public VisionEngineDescriptor(
        Guid engineInstanceId,
        VisionEngineSelection selection,
        IEnumerable<VisionOperationCapability> operations)
    {
        if (engineInstanceId == Guid.Empty)
        {
            throw new ArgumentException("引擎身份不能为空。", nameof(engineInstanceId));
        }

        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(operations);
        var snapshot = operations.ToArray();
        if (snapshot.Any(operation => operation == null))
        {
            throw new ArgumentException("能力不能包含空条目。", nameof(operations));
        }

        if (snapshot.GroupBy(operation => operation.OperationId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("操作身份重复。", nameof(operations));
        }

        if (selection.AiBackendId == null
            && snapshot.Any(operation => operation.Domain == VisionExecutionDomain.Ai))
        {
            throw new ArgumentException("未启用AI，不能声明AI能力。", nameof(operations));
        }

        EngineInstanceId = engineInstanceId;
        Selection = selection;
        Operations = Array.AsReadOnly(snapshot);
    }
}
