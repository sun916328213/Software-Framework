// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Regions」）。
//   轮廓（Contour）= 区域或 XLD 的边界折线，用于显示、测量与形状拟合。
//   ⛔ 与旧 Vision.Core.Vision.I_XLD 的关系：I_XLD 是**带显示属性 + WPF Point** 的旧契约
//      （P7 统一），本接口是纯几何的**目标契约**，P2 先立着，P7 再谈迁移。
namespace Vision.Core.Regions
{
    /// <summary>
    /// 轮廓契约 —— 用折线点列表达一条（或一组）边界。
    /// </summary>
    public interface IVisionContour
    {
        /// <summary>是否闭合（首尾相连）。</summary>
        bool IsClosed { get; }

        /// <summary>顶点个数。</summary>
        int PointCount { get; }

        /// <summary>顶点序列（图像坐标系）。</summary>
        System.Collections.Generic.IReadOnlyList<Geometry.Point2D> Points { get; }

        /// <summary>轴对齐外接框。</summary>
        Geometry.Rect2D Bounds { get; }

        /// <summary>折线总长度（闭合轮廓含收尾一段）。</summary>
        double Length { get; }
    }
}
