#nullable enable

namespace Vision.Core.Operations;

/// <summary>像素邻域超出图像时的明确取值规则；不依赖SDK默认值。</summary>
public enum ImageBorderMode
{
    /// <summary>图像外补零。</summary>
    ConstantZero = 0,

    /// <summary>复制最近的边界像素。</summary>
    Replicate = 1,

    /// <summary>镜像但不重复最外层边界像素。</summary>
    Reflect101 = 2
}
