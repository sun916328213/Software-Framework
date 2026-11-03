namespace Vision.Core.Operations;

/// <summary>线性输出已限幅为非负后使用的量化规则。</summary>
public enum GrayLinearRounding
{
    /// <summary>保留所选后端既有最近整数规则，不承诺跨厂商同值。</summary>
    BackendNearest,
    /// <summary>按计算所得非负double取最近整数；精确中点向更大整数，紧邻中点的值不因加法而提前进位。</summary>
    HalfUp
}
