using System.Text.Json.Serialization;

namespace Vision.Core.Regions
{
    /// <summary>
    /// 区域表示形式。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RegionKind
    {
        /// <summary>
        /// 未指定。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 轴对齐矩形区域。
        /// </summary>
        Rectangle,

        /// <summary>
        /// 椭圆（含圆）区域。
        /// </summary>
        Ellipse,

        /// <summary>
        /// 多边形区域。
        /// </summary>
        Polygon,

        /// <summary>
        /// 位图掩膜（逐像素 0/1）。
        /// </summary>
        Mask,

        /// <summary>
        /// 行程编码（每行若干 [起始列, 长度] 段）。
        /// </summary>
        RunLength,
    }

    /// <summary>
    /// 算法输出区域的只读几何描述。
    /// 实现负责区域存储和资源管理，调用者只读取形状、边界和面积。
    /// </summary>
    public interface IVisionRegion
    {
        /// <summary>
        /// 表示形式。
        /// </summary>
        RegionKind Kind
        {
            get;
        }

        /// <summary>
        /// 轴对齐外接框（图像坐标系，像素）。
        /// </summary>
        Geometry.Rect2D Bounds
        {
            get;
        }

        /// <summary>
        /// 面积（像素个数）。
        /// </summary>
        double Area
        {
            get;
        }

        /// <summary>
        /// 是否为空区域。
        /// </summary>
        bool IsEmpty
        {
            get;
        }
    }
}
