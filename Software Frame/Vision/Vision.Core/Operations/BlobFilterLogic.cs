#nullable enable

namespace Vision.Core.Operations;

/// <summary>多个区域特征范围的组合关系；空范围集合不过滤。</summary>
public enum BlobFilterLogic
{
    /// <summary>全部满足。</summary>
    All = 0,

    /// <summary>至少满足一个。</summary>
    Any = 1
}
