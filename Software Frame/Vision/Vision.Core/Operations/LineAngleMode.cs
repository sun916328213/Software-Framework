#nullable enable

namespace Vision.Core.Operations;

/// <summary>直线角度的方向/周期语义，必须明确选择。</summary>
public enum LineAngleMode
{
    /// <summary>无向锐夹角，范围0..90度。</summary>
    UndirectedAcute = 0,

    /// <summary>按Start→End方向的屏幕顺时针有符号角，范围(-180,180]度。</summary>
    Directed = 1
}
