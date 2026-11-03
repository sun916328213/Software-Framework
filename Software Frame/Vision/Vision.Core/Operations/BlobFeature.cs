#nullable enable

namespace Vision.Core.Operations;

/// <summary>区域像素特征与像素中心轮廓几何分开命名，不把两种面积口径混用。</summary>
public enum BlobFeature
{
    /// <summary>前景像素计数，单位像素面积。</summary>
    Area = 0,

    /// <summary>轴对齐外接框宽度，单位像素。</summary>
    Width = 1,

    /// <summary>轴对齐外接框高度，单位像素。</summary>
    Height = 2,

    /// <summary>像素面积除以外接框宽高积。</summary>
    Extent = 3,

    /// <summary>像素轮廓内的孔洞数量。</summary>
    HoleCount = 4,

    /// <summary>闭合轮廓多边形净面积：外边界面积减孔洞面积，单位像素平方；不是像素计数。</summary>
    ContourArea = 5,

    /// <summary>外边界与孔洞的像素中心闭合折线长度之和，单位像素。</summary>
    ContourPerimeter = 6,

    /// <summary>4π×轮廓净面积/总周长平方；退化轮廓无定义，不满足任何该特征范围。</summary>
    ContourCircularity = 7
}
