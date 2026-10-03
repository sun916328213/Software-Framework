// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：Point2D。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Geometry」）。
//   ⛔ 本命名空间里**只允许**用 double 表达的纯几何量。
//      这正是与 Vision.Core.Vision（旧 I_ROI / I_XLD）的关键差别 —— 后者用 System.Windows.Point，
//      把 WPF 拖进了 Core。ROI / 坐标的实质统一是 **P7** 的范围（计划 §P7），
//      P2 只把「不依赖 WPF 的目标类型」先立起来，旧类型本轮不动。
namespace Vision.Core.Geometry
{
    /// <summary>
    /// 二维点（图像/物理坐标，单位由上下文决定）。
    /// 用 <see cref="double"/> 而非 <c>float</c>：标定链上会连乘多个变换矩阵，float 精度不够。
    /// </summary>
    public struct Point2D : IEquatable<Point2D>
    {
        /// <summary>X 分量。</summary>
        public double X { get; set; }

        /// <summary>Y 分量。</summary>
        public double Y { get; set; }

        /// <summary>构造一个点。</summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>原点 (0, 0)。</summary>
        public static Point2D Origin => new Point2D(0, 0);

        /// <summary>到另一点的欧氏距离。</summary>
        /// <param name="other">目标点。</param>
        /// <returns>距离。</returns>
        public double DistanceTo(Point2D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>按增量平移。</summary>
        /// <param name="dx">X 增量。</param>
        /// <param name="dy">Y 增量。</param>
        /// <returns>新点。</returns>
        public Point2D Offset(double dx, double dy) => new Point2D(X + dx, Y + dy);

        /// <summary>与另一点的中点。</summary>
        /// <param name="other">目标点。</param>
        /// <returns>中点。</returns>
        public Point2D Midpoint(Point2D other) => new Point2D((X + other.X) * 0.5, (Y + other.Y) * 0.5);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Equals。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </remarks>
        public bool Equals(Point2D other) => X.Equals(other.X) && Y.Equals(other.Y);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Equals。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </remarks>
        public override bool Equals(object obj) => obj is Point2D other && Equals(other);

        /// <inheritdoc/>
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetHashCode。
        /// 正文中的关键协作调用包括 GetHashCode；具体条件和先后次序见方法体。
        /// </remarks>
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() => $"({X:0.####}, {Y:0.####})";

        /// <summary>值相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>相等时为 true。</returns>
        public static bool operator ==(Point2D left, Point2D right) => left.Equals(right);

        /// <summary>值不等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>不等时为 true。</returns>
        public static bool operator !=(Point2D left, Point2D right) => !left.Equals(right);
    }
}
