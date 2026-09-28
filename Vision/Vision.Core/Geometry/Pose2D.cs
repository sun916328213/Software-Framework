// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Geometry」）。
namespace Vision.Core.Geometry
{
    /// <summary>
    /// 二维刚体位姿（平移 + 旋转），视觉里 Fixture / 标定 / 定位结果的统一载体。
    ///
    /// <b>语义约定（务必先读再改）</b>：一个 <see cref="Pose2D"/> 描述「本坐标系 → 父坐标系」的变换，
    /// <code>
    /// p_parent = R(AngleDeg) · p_local + (X, Y)
    /// </code>
    /// 即：<b>先绕原点旋转，再平移</b>；角度<b>逆时针为正</b>、单位为<b>度</b>。
    /// 图像坐标系 Y 轴朝下时，「逆时针为正」在屏幕上看起来是顺时针 —— 这是刻意与
    /// HALCON <c>vector_angle_to_rigid</c> / <c>hom_mat2d_rotate</c> 的数学约定保持一致，
    /// 免得 P3 迁移时符号对不上。
    /// </summary>
    public struct Pose2D : IEquatable<Pose2D>
    {
        /// <summary>平移 X。</summary>
        public double X { get; set; }

        /// <summary>平移 Y。</summary>
        public double Y { get; set; }

        /// <summary>旋转角（度，逆时针为正）。</summary>
        public double AngleDeg { get; set; }

        /// <summary>构造一个位姿。</summary>
        /// <param name="x">平移 X。</param>
        /// <param name="y">平移 Y。</param>
        /// <param name="angleDeg">旋转角（度）。</param>
        public Pose2D(double x, double y, double angleDeg)
        {
            X = x;
            Y = y;
            AngleDeg = angleDeg;
        }

        /// <summary>单位位姿（不平移不旋转）。</summary>
        public static Pose2D Identity => default;

        /// <summary>是否为单位位姿（角度按 360° 归一后判断）。</summary>
        public bool IsIdentity =>
            X == 0 && Y == 0 && Math.Abs(NormalizeAngle(AngleDeg)) < 1e-12;

        /// <summary>旋转角（弧度）。</summary>
        public double AngleRad => AngleDeg * Math.PI / 180.0;

        /// <summary>把本坐标系下的点变换到父坐标系。</summary>
        /// <param name="local">本坐标系下的点。</param>
        /// <returns>父坐标系下的点。</returns>
        public Point2D Transform(Point2D local)
        {
            var rad = AngleRad;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            return new Point2D(
                X + local.X * cos - local.Y * sin,
                Y + local.X * sin + local.Y * cos);
        }

        /// <summary>把父坐标系下的点反变换回本坐标系（<see cref="Inverse"/> 的便捷形式）。</summary>
        /// <param name="parent">父坐标系下的点。</param>
        /// <returns>本坐标系下的点。</returns>
        public Point2D InverseTransform(Point2D parent) => Inverse().Transform(parent);

        /// <summary>求逆位姿（父坐标系 → 本坐标系）。</summary>
        /// <returns>逆位姿。</returns>
        public Pose2D Inverse()
        {
            var rad = AngleRad;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            return new Pose2D(
                -(X * cos + Y * sin),
                X * sin - Y * cos,
                -AngleDeg);
        }

        /// <summary>
        /// 级联：<c>this ∘ child</c> —— 把 <paramref name="child"/>（子坐标系 → 本坐标系）
        /// 接到本 pose 后面，得到「子坐标系 → 父坐标系」的合成位姿。
        /// </summary>
        /// <param name="child">子级位姿。</param>
        /// <returns>合成位姿。</returns>
        public Pose2D Compose(Pose2D child)
        {
            var rad = AngleRad;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            return new Pose2D(
                X + child.X * cos - child.Y * sin,
                Y + child.X * sin + child.Y * cos,
                AngleDeg + child.AngleDeg);
        }

        /// <summary>把角度归一化到 (-180, 180]。</summary>
        /// <param name="angleDeg">任意角度（度）。</param>
        /// <returns>归一化后的角度。</returns>
        public static double NormalizeAngle(double angleDeg)
        {
            var normalized = angleDeg % 360.0;
            if (normalized > 180.0)
                normalized -= 360.0;
            if (normalized <= -180.0)
                normalized += 360.0;
            return normalized;
        }

        /// <inheritdoc/>
        public bool Equals(Pose2D other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && AngleDeg.Equals(other.AngleDeg);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Pose2D other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ AngleDeg.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() =>
            $"Pose2D(x={X:0.####}, y={Y:0.####}, θ={AngleDeg:0.####}°)";

        /// <summary>值相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>相等时为 true。</returns>
        public static bool operator ==(Pose2D left, Pose2D right) => left.Equals(right);

        /// <summary>值不等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>不等时为 true。</returns>
        public static bool operator !=(Pose2D left, Pose2D right) => !left.Equals(right);
    }
}
