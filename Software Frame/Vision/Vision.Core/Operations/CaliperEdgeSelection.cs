#nullable enable

namespace Vision.Core.Operations;

/// <summary>每把卡尺至多选择一个局部峰，不把多边缘隐式合并成边缘对。</summary>
public enum CaliperEdgeSelection
{
    /// <summary>按扫描次序选择第一个峰。</summary>
    First = 0,

    /// <summary>按扫描次序选择最后一个峰。</summary>
    Last = 1,

    /// <summary>选择绝对对比度最大峰，并列取扫描次序较早者。</summary>
    Strongest = 2
}
