#nullable enable

namespace Vision.Core.Models;

/// <summary>
/// 厂商中立模型定义的根类型；不持SDK句柄、不执行加载/训练。
/// </summary>
public abstract class VisionModelDefinition
{
    /// <summary>
    /// 模型种类。
    /// </summary>
    public abstract VisionModelKind Kind
    {
        get;
    }
}

