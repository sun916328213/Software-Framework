#nullable enable

namespace Vision.Core.Operations;

/// <summary>二值形态学方式，不包含隐式阈值或灰度形态学。</summary>
public enum MorphologyKind
{
    /// <summary>腐蚀。</summary>
    Erode = 0,

    /// <summary>膨胀。</summary>
    Dilate = 1,

    /// <summary>先腐蚀后膨胀，去除较小前景。</summary>
    Open = 2,

    /// <summary>先膨胀后腐蚀，填补较小孔隙。</summary>
    Close = 3
}
