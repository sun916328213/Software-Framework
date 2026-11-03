#nullable enable

namespace Vision.Core.Operations;

/// <summary>结果内排序；最终仍按行、列和组件标签打破并列，不代表跨帧跟踪身份。</summary>
public enum BlobSort
{
    /// <summary>面积由大到小。</summary>
    AreaDescending = 0,

    /// <summary>面积由小到大。</summary>
    AreaAscending = 1,

    /// <summary>质心X由小到大。</summary>
    XAscending = 2,

    /// <summary>质心Y由小到大。</summary>
    YAscending = 3
}
