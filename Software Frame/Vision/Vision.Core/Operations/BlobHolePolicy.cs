#nullable enable

namespace Vision.Core.Operations;

/// <summary>连通域阈值化之后的孔洞策略，不依赖厂商默认值。</summary>
public enum BlobHolePolicy
{
    /// <summary>保留孔洞，面积不计孔内背景。</summary>
    Preserve = 0,

    /// <summary>填充不与图像外部连通的背景孔洞，参与域外仍为背景。</summary>
    Fill = 1
}
