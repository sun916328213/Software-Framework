#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 从明确的绝对路径解码一幅文件主图，不借用或创建假输入帧。
/// 使用文件存储方向，不自动按EXIF旋转；不遍历动画或多页，不恢复标定。
/// Core只保存请求值，不读文件；编码和像素格式支持范围由所选实现校验。
/// </summary>
public sealed class ImageFileOperation : VisionOperation<ImageFileOutput>
{
    public const string Id = "vision.acquisition.image-file";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    /// <summary>已规范化的绝对文件路径，不依赖执行时的当前目录。</summary>
    public string FilePath
    {
        get;
    }

    public ImageFileOperation(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException("图像文件必须使用明确的绝对路径。", nameof(filePath));
        }

        FilePath = Path.GetFullPath(filePath);
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
