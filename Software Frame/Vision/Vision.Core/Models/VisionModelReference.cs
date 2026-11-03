#nullable enable
using Vision.Core.Engine;

namespace Vision.Core.Models;

/// <summary>
/// 仅在当前引擎/上下文有效的准备结果；不持久化为可跨后端复用的原生模型。
/// </summary>
/// <remarks>身份不是安全凭证，Tools仍必须核验模型是否实际存在、未释放且内容与修订一致。</remarks>
public sealed class VisionModelReference
{
    /// <summary>
    /// 模型所属的工具上下文。
    /// </summary>
    public VisionContext Context
    {
        get;
    }

    /// <summary>
    /// 本次模型实例的唯一标识。
    /// </summary>
    public Guid ModelId
    {
        get;
    }

    /// <summary>
    /// 创建该模型的实现标识。
    /// </summary>
    public string BackendId
    {
        get;
    }

    /// <summary>
    /// 模型准备时使用的定义快照。
    /// </summary>
    public VisionModelDefinition Definition
    {
        get;
    }

    /// <summary>
    /// 调用者提供的非负配方修订号。
    /// </summary>
    public long Revision
    {
        get;
    }

    /// <summary>
    /// 创建模型引用标识，实际存活和归属仍由实现核验。
    /// </summary>
    public VisionModelReference(
        VisionContext context,
        Guid modelId,
        string backendId,
        VisionModelDefinition definition,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (modelId == Guid.Empty)
        {
            throw new ArgumentException("模型身份不能为空。", nameof(modelId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(backendId);
        ArgumentNullException.ThrowIfNull(definition);
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Context = context;
        ModelId = modelId;
        BackendId = backendId;
        Definition = definition;
        Revision = revision;
    }
}
