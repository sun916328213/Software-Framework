using System.Text.Json.Serialization;

namespace Vision.Core.Roi
{
    /// <summary>
    /// ROI 形状种类。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoiKind
    {
        /// <summary>
        /// 未指定形状。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 轴对齐矩形。
        /// </summary>
        Rectangle,

        /// <summary>
        /// 带角度的矩形。
        /// </summary>
        RotatedRectangle,

        /// <summary>
        /// 圆形。
        /// </summary>
        Circle,

        /// <summary>
        /// 椭圆。
        /// </summary>
        Ellipse,

        /// <summary>
        /// 多边形。
        /// </summary>
        Polygon,

        /// <summary>
        /// 位图掩膜。
        /// </summary>
        Mask,
    }

    /// <summary>
    /// ROI 契约 —— 算法输入侧的「感兴趣区域」。
    ///
    /// 统一用 <see cref="Pose"/> + <see cref="Size"/> 表达几何，而不是每种形状各存一套字段：
    /// <list type="bullet">
    ///   <item><see cref="RoiKind.Rectangle"/>：<c>Pose.AngleDeg == 0</c> 的矩形；</item>
    ///   <item><see cref="RoiKind.RotatedRectangle"/>：带角度的矩形；</item>
    ///   <item><see cref="RoiKind.Circle"/>/<see cref="RoiKind.Ellipse"/>：<see cref="Size"/> 为直径；</item>
    ///   <item><see cref="RoiKind.Polygon"/>：几何看 <see cref="Outline"/>，<see cref="Pose"/>/<see cref="Size"/> 为其外接框。</item>
    /// </list>
    /// 定位跟随通过变换 <see cref="Pose"/> 表达，具体轮廓由区域形状决定。
    /// </summary>
    public interface IRoi
    {
        /// <summary>
        /// 形状种类。
        /// </summary>
        RoiKind Kind
        {
            get;
        }

        /// <summary>
        /// 本 ROI 在图像坐标系中的位姿（位置 + 角度）。
        /// </summary>
        Geometry.Pose2D Pose
        {
            get;
        }

        /// <summary>
        /// 本 ROI 自身坐标系下的尺寸；圆/椭圆时宽高即直径。
        /// </summary>
        Geometry.Size2D Size
        {
            get;
        }

        /// <summary>
        /// 轴对齐外接框（图像坐标系）。
        /// </summary>
        Geometry.Rect2D Bounds
        {
            get;
        }

        /// <summary>
        /// 轮廓顶点（图像坐标系）。
        /// 多边形返回真实顶点；其余形状返回实现方采样出的闭合轮廓（采样密度由实现方决定，
        /// 只用于显示与粗判，不用于精确测量）。
        /// </summary>
        System.Collections.Generic.IReadOnlyList<Geometry.Point2D> Outline
        {
            get;
        }

        /// <summary>
        /// 是否为空（未初始化）ROI。
        /// </summary>
        bool IsEmpty
        {
            get;
        }
    }
}
