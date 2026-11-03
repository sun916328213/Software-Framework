#nullable enable

namespace Vision.Core.Engine;

/// <summary>
/// 中立上下文身份，不含SDK会话或原生句柄；不是安全令牌，实现仍须验证实际注册状态。
/// </summary>
public sealed record VisionContext
{
    /// <summary>
    /// 所属引擎实例的唯一标识。
    /// </summary>
    public Guid EngineInstanceId
    {
        get;
    }

    /// <summary>
    /// 当前工具上下文的唯一标识。
    /// </summary>
    public Guid ContextId
    {
        get;
    }

    /// <summary>
    /// 创建具有明确引擎归属的工具上下文标识。
    /// </summary>
    public VisionContext(
        Guid engineInstanceId,
        Guid contextId)
    {
        if (engineInstanceId == Guid.Empty)
        {
            throw new ArgumentException("引擎实例身份不能为空。", nameof(engineInstanceId));
        }

        if (contextId == Guid.Empty)
        {
            throw new ArgumentException("上下文身份不能为空。", nameof(contextId));
        }

        EngineInstanceId = engineInstanceId;
        ContextId = contextId;
    }
}
