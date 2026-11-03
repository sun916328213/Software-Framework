#nullable enable

namespace Vision.Core.Operations;

/// <summary>基础灰度滤波方式；方法之间不隐式替换。</summary>
public enum ImageFilterKind
{
    /// <summary>均值滤波。</summary>
    Mean = 0,

    /// <summary>使用明确sigma的高斯滤波。</summary>
    Gaussian = 1,

    /// <summary>中值滤波；边界支持由具体实现核对。</summary>
    Median = 2
}
