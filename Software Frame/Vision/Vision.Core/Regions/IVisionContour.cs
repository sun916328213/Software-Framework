namespace Vision.Core.Regions
{
    /// <summary>
    /// 轮廓契约 —— 用折线点列表达一条（或一组）边界。
    /// </summary>
    public interface IVisionContour
    {
        /// <summary>
        /// 是否闭合（首尾相连）。
        /// </summary>
        bool IsClosed
        {
            get;
        }

        /// <summary>
        /// 顶点个数。
        /// </summary>
        int PointCount
        {
            get;
        }

        /// <summary>
        /// 顶点序列（图像坐标系）。
        /// </summary>
        System.Collections.Generic.IReadOnlyList<Geometry.Point2D> Points
        {
            get;
        }

        /// <summary>
        /// 轴对齐外接框。
        /// </summary>
        Geometry.Rect2D Bounds
        {
            get;
        }

        /// <summary>
        /// 折线总长度（闭合轮廓含收尾一段）。
        /// </summary>
        double Length
        {
            get;
        }
    }
}
