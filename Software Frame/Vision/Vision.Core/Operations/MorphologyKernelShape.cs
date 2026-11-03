#nullable enable

namespace Vision.Core.Operations;

/// <summary>中心锚点结构元素的栅格形状。</summary>
public enum MorphologyKernelShape
{
    /// <summary>矩形结构元素。</summary>
    Rectangle = 0,

    /// <summary>栅格椭圆结构元素。</summary>
    Ellipse = 1,

    /// <summary>中心行列组成的十字结构元素。</summary>
    Cross = 2
}
