using System.Text.Json.Serialization;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Roi」）。
//   ⛔ 与旧 Vision.Core.Vision 的 I_ROI / E_ROIType 的关系（务必先读）：
//      旧契约（I_ROIs.cs）用 System.Windows.Point 表达顶点 —— 那是把 WPF 拖进 Core 的
//      三处残留之一。计划把「ROI / Fixture / Coordinate 统一」放在 **P7**，P2 不动旧类型，
//      只把**目标形态**立在这里：纯 double 几何（Vision.Core.Geometry），零 WPF。
//      E_ROIType 与 RoiKind 的对应关系见下面每个成员的注释，P7 迁移时照此映射。
namespace Vision.Core.Roi
{
    /// <summary>
    /// ROI 形状种类。
    /// 取值刻意与旧 <c>Vision.Core.Vision.E_ROIType</c> 一一对应
    /// （Null→<see cref="Unknown"/>、Rectangle→<see cref="Rectangle"/>、
    /// Rectangle2→<see cref="RotatedRectangle"/>、Circle→<see cref="Circle"/>、
    /// Ellipsoid→<see cref="Ellipse"/>、Polygon→<see cref="Polygon"/>），
    /// 只把含混的名字改清楚。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoiKind
    {
        /// <summary>未指定（对应旧 <c>E_ROIType.Null</c>）。</summary>
        Unknown = 0,

        /// <summary>轴对齐矩形（对应旧 <c>Rectangle</c>）。</summary>
        Rectangle,

        /// <summary>带角度的矩形（对应旧 <c>Rectangle2</c>）。</summary>
        RotatedRectangle,

        /// <summary>圆（对应旧 <c>Circle</c>）。</summary>
        Circle,

        /// <summary>椭圆（对应旧 <c>Ellipsoid</c>）。</summary>
        Ellipse,

        /// <summary>多边形（对应旧 <c>Polygon</c>）。</summary>
        Polygon,

        /// <summary>位图掩膜（旧契约没有，OpenCV 后端需要）。</summary>
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
    /// 这样 P7「ROI 可跟随 Fixture」只需变换 <see cref="Pose"/>，不必按形状分支。
    /// </summary>
    public interface IRoi
    {
        /// <summary>形状种类。</summary>
        RoiKind Kind { get; }

        /// <summary>本 ROI 在图像坐标系中的位姿（位置 + 角度）。</summary>
        Geometry.Pose2D Pose { get; }

        /// <summary>本 ROI 自身坐标系下的尺寸；圆/椭圆时宽高即直径。</summary>
        Geometry.Size2D Size { get; }

        /// <summary>轴对齐外接框（图像坐标系）。</summary>
        Geometry.Rect2D Bounds { get; }

        /// <summary>
        /// 轮廓顶点（图像坐标系）。
        /// 多边形返回真实顶点；其余形状返回实现方采样出的闭合轮廓（采样密度由实现方决定，
        /// 只用于显示与粗判，不用于精确测量）。
        /// </summary>
        System.Collections.Generic.IReadOnlyList<Geometry.Point2D> Outline { get; }

        /// <summary>是否为空（未初始化）ROI。</summary>
        bool IsEmpty { get; }
    }
}
