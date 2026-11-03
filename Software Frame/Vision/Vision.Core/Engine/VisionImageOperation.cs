#nullable enable
using Vision.Core.Imaging;
using Vision.Core.Models;

namespace Vision.Core.Engine;

/// <summary>
/// 必须具有图像输入的强类型请求。与无图数值请求分开，不允许空图或虚假帧适配。
/// </summary>
public abstract class VisionImageOperation<TResult> : VisionOperation<TResult> where TResult : class
{
    /// <summary>本次实际使用的非空、独立图像快照。</summary>
    public sealed override VisionFrame Image
    {
        get;
    }

    protected VisionImageOperation(VisionFrame image, VisionModelReference? model = null)
        : base(model)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }
}
