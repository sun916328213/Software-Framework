#nullable enable

namespace Vision.Core.Capabilities;

/// <summary>
/// 视觉操作所属的执行域，每个执行域只选择一个实现。
/// </summary>
public enum VisionExecutionDomain
{
    /// <summary>
    /// 传统视觉处理与测量。
    /// </summary>
    Vision,

    /// <summary>
    /// 人工智能模型推理。
    /// </summary>
    Ai
}
