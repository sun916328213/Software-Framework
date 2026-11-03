#nullable enable

namespace Vision.Core.Models;

/// <summary>
/// 模型资源的中立位置和内容指纹。Tools读取并核验内容，Core不执行文件I/O。
/// </summary>
public sealed class AiClassificationModelDefinition : VisionModelDefinition
{
    /// <summary>
    /// 模型种类。
    /// </summary>
    public override VisionModelKind Kind => VisionModelKind.AiClassification;

    /// <summary>
    /// 模型资源位置；实际读取由实现负责。
    /// </summary>
    public string Location
    {
        get;
    }

    /// <summary>
    /// 模型内容的 SHA256 指纹，用于校验资源是否与配方一致。
    /// </summary>
    public string ContentSha256
    {
        get;
    }

    /// <summary>
    /// 创建分类模型定义，校验资源位置和内容指纹格式。
    /// </summary>
    public AiClassificationModelDefinition(
        string location,
        string contentSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentNullException.ThrowIfNull(contentSha256);
        if (contentSha256.Length != 64 || contentSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("必须提供模型内容的SHA256，不以可变文件路径代替模型身份。", nameof(contentSha256));
        }

        Location = location;
        ContentSha256 = contentSha256.ToLowerInvariant();
    }
}
