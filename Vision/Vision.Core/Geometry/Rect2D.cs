// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Geometry」）。
namespace Vision.Core.Geometry
{
    /// <summary>
    /// 轴对齐矩形。用「左上角 + 宽高」表示（与 WPF <c>Rect</c>、HALCON <c>gen_rectangle1</c> 一致），
    /// 而不是「左上 + 右下」—— 后者在宽高为负时语义含混。
    /// </summary>
    public struct Rect2D : IEquatable<Rect2D>
    {
        /// <summary>左上角 X。</summary>
        public double X { get; set; }

        /// <summary>左上角 Y。</summary>
        public double Y { get; set; }

        /// <summary>宽。</summary>
        public double Width { get; set; }

        /// <summary>高。</summary>
        public double Height { get; set; }

        /// <summary>构造一个矩形。</summary>
        /// <param name="x">左上角 X。</param>
        /// <param name="y">左上角 Y。</param>
        /// <param name="width">宽。</param>
        /// <param name="height">高。</param>
        public Rect2D(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>空矩形（默认值）。</summary>
        public static Rect2D Empty => default;

        /// <summary>左边界。</summary>
        public double Left => X;

        /// <summary>上边界。</summary>
        public double Top => Y;

        /// <summary>右边界。</summary>
        public double Right => X + Width;

        /// <summary>下边界。</summary>
        public double Bottom => Y + Height;

        /// <summary>中心点。</summary>
        public Point2D Center => new Point2D(X + Width * 0.5, Y + Height * 0.5);

        /// <summary>左上角。</summary>
        public Point2D TopLeft => new Point2D(X, Y);

        /// <summary>尺寸。</summary>
        public Size2D Size => new Size2D(Width, Height);

        /// <summary>面积。</summary>
        public double Area => Width * Height;

        /// <summary>任一维非正即为空。</summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>用四条边界构造（右下为开区间边界，与 <see cref="Right"/>/<see cref="Bottom"/> 同义）。</summary>
        /// <param name="left">左边界。</param>
        /// <param name="top">上边界。</param>
        /// <param name="right">右边界。</param>
        /// <param name="bottom">下边界。</param>
        /// <returns>构造好的矩形。</returns>
        public static Rect2D FromLTRB(double left, double top, double right, double bottom) =>
            new Rect2D(left, top, right - left, bottom - top);

        /// <summary>点是否落在矩形内（含边界）。</summary>
        /// <param name="point">待判定点。</param>
        /// <returns>在内时为 true。</returns>
        public bool Contains(Point2D point) => Contains(point.X, point.Y);

        /// <summary>点是否落在矩形内（含边界）。</summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        /// <returns>在内时为 true。</returns>
        public bool Contains(double x, double y) =>
            !IsEmpty && x >= Left && x <= Right && y >= Top && y <= Bottom;

        /// <summary>四周同时扩张（负值为收缩）。</summary>
        /// <param name="dx">左右各扩张量。</param>
        /// <param name="dy">上下各扩张量。</param>
        /// <returns>新矩形。</returns>
        public Rect2D Inflate(double dx, double dy) =>
            new Rect2D(X - dx, Y - dy, Width + dx * 2, Height + dy * 2);

        /// <summary>与另一矩形是否相交（边贴边算相交）。</summary>
        /// <param name="other">另一矩形。</param>
        /// <returns>相交时为 true。</returns>
        public bool IntersectsWith(Rect2D other) =>
            !IsEmpty && !other.IsEmpty &&
            other.Left <= Right && other.Right >= Left &&
            other.Top <= Bottom && other.Bottom >= Top;

        /// <summary>求交集；不相交时返回 <see cref="Empty"/>。</summary>
        /// <param name="other">另一矩形。</param>
        /// <returns>交集矩形。</returns>
        public Rect2D Intersect(Rect2D other)
        {
            if (!IntersectsWith(other))
                return Empty;

            var left = Math.Max(Left, other.Left);
            var top = Math.Max(Top, other.Top);
            var right = Math.Min(Right, other.Right);
            var bottom = Math.Min(Bottom, other.Bottom);
            return FromLTRB(left, top, right, bottom);
        }

        /// <inheritdoc/>
        public bool Equals(Rect2D other) =>
            X.Equals(other.X) && Y.Equals(other.Y) &&
            Width.Equals(other.Width) && Height.Equals(other.Height);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Rect2D other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            (((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Width.GetHashCode()) * 397 ^ Height.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() =>
            IsEmpty ? "Rect2D(empty)" : $"Rect2D({X:0.####}, {Y:0.####}, {Width:0.####}×{Height:0.####})";

        /// <summary>值相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>相等时为 true。</returns>
        public static bool operator ==(Rect2D left, Rect2D right) => left.Equals(right);

        /// <summary>值不等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>不等时为 true。</returns>
        public static bool operator !=(Rect2D left, Rect2D right) => !left.Equals(right);
    }
}
