// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AffineTransform2D。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// ─────────────────────────────────────────────────────────────────────────────
// P7 —— 二维仿射变换（计划 §P7「建立 Image -> Fixture -> Calibration -> Machine 坐标链」）。
//
// 为什么需要仿射：Pose2D 只表达刚体（旋转+平移），标定链里还有像素当量缩放、
// 轴间不正交（shear），必须用 3×3 齐次矩阵。存 6 个有效参数（末行恒为 0,0,1）。
// 語義：p' = (A*x + B*y + Tx, C*x + D*y + Ty)。
//
// 与 HALCON HomMat2d 的互操作：行主序 "a,b,c|d,e,f|0,0,1"（FromHalconMatrixString /
// ToMatrixString），与 Tools.Halcon 矩阵串格式一致，P7 测试做往返断言。
// 纯 BCL/Core 类型，可无 Runtime 测试。
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json.Serialization;

namespace Vision.Core.Coordinate
{
    /// <summary>
    /// 二维仿射变换（齐次 3×3，前两行）。不可逆（行列式≈0）时反变换抛异常。
    /// </summary>
    public struct AffineTransform2D : IEquatable<AffineTransform2D>
    {
        /// <summary>x' 行的 x 系数。</summary>
        public double A { get; set; }

        /// <summary>x' 行的 y 系数。</summary>
        public double B { get; set; }

        /// <summary>x' 行的平移。</summary>
        public double Tx { get; set; }

        /// <summary>y' 行的 x 系数。</summary>
        public double C { get; set; }

        /// <summary>y' 行的 y 系数。</summary>
        public double D { get; set; }

        /// <summary>y' 行的平移。</summary>
        public double Ty { get; set; }

        /// <summary>构造一个仿射变换。</summary>
        public AffineTransform2D(double a, double b, double tx, double c, double d, double ty)
        {
            A = a; B = b; Tx = tx; C = c; D = d; Ty = ty;
        }

        /// <summary>单位变换。</summary>
        public static AffineTransform2D Identity => new(1, 0, 0, 0, 1, 0);

        /// <summary>行列式（ad-bc）；≈0 时不可逆。</summary>
        [JsonIgnore]
        public double Determinant => A * D - B * C;

        /// <summary>是否可逆。</summary>
        [JsonIgnore]
        public bool IsInvertible => Math.Abs(Determinant) > 1e-12;

        /// <summary>正向映射。</summary>
        public Geometry.Point2D Transform(Geometry.Point2D point) => new(
            A * point.X + B * point.Y + Tx,
            C * point.X + D * point.Y + Ty);

        /// <summary>反映射；奇异时抛 <see cref="InvalidOperationException"/>。</summary>
        public Geometry.Point2D TransformInverse(Geometry.Point2D point) => Inverse().Transform(point);

        /// <summary>求逆；奇异时抛 <see cref="InvalidOperationException"/>。</summary>
        public AffineTransform2D Inverse()
        {
            var det = Determinant;
            if (Math.Abs(det) <= 1e-12)
                throw new InvalidOperationException("仿射变换奇异（行列式≈0），不可逆。");
            return new AffineTransform2D(
                D / det, -B / det, (B * Ty - D * Tx) / det,
                -C / det, A / det, (C * Tx - A * Ty) / det);
        }

        /// <summary>
        /// 级联：<c>this ∘ child</c> —— 先施加 <paramref name="child"/>，再施加本变换
        /// （与 <see cref="Geometry.Pose2D.Compose"/> 同约定）。
        /// </summary>
        public AffineTransform2D Compose(AffineTransform2D child) => new(
            A * child.A + B * child.C,
            A * child.B + B * child.D,
            A * child.Tx + B * child.Ty + Tx,
            C * child.A + D * child.C,
            C * child.B + D * child.D,
            C * child.Tx + D * child.Ty + Ty);

        /// <summary>由刚体位姿构造（约定与 Pose2D 一致：先绕原点旋转再平移）。</summary>
        public static AffineTransform2D FromPose(Geometry.Pose2D pose)
        {
            var cos = Math.Cos(pose.AngleRad);
            var sin = Math.Sin(pose.AngleRad);
            return new AffineTransform2D(cos, -sin, pose.X, sin, cos, pose.Y);
        }

        /// <summary>
        /// 尝试还原为刚体位姿（列向量标准正交时成功；含缩放/shear 时失败）。
        /// </summary>
        public bool TryToPose(out Geometry.Pose2D pose, double tolerance = 1e-9)
        {
            pose = default;
            if (Math.Abs(A * A + C * C - 1) > tolerance) return false;
            if (Math.Abs(B * B + D * D - 1) > tolerance) return false;
            if (Math.Abs(A * B + C * D) > tolerance) return false;
            if (Determinant <= 0) return false;
            pose = new Geometry.Pose2D(Tx, Ty, Math.Atan2(C, A) * 180.0 / Math.PI);
            return true;
        }

        /// <summary>
        /// 是否为相似变换（保角：列向量等长正交）；是则给出均匀缩放与旋转角（度）。
        /// ROI/框变换用它决定形状保持还是退化为多边形。
        /// </summary>
        public bool IsSimilarity(double tolerance, out double scale, out double angleDeg)
        {
            scale = 0;
            angleDeg = 0;
            var sx = Math.Sqrt(A * A + C * C);
            var sy = Math.Sqrt(B * B + D * D);
            if (sx <= tolerance || sy <= tolerance) return false;
            if (Math.Abs(sx - sy) / Math.Max(sx, sy) > tolerance) return false;
            if (Math.Abs(A * B + C * D) / (sx * sy) > tolerance) return false;
            if (Determinant <= 0) return false;
            scale = (sx + sy) * 0.5;
            angleDeg = Math.Atan2(C, A) * 180.0 / Math.PI;
            return true;
        }

        /// <summary>
        /// 由“像素↔世界”点对最小二乘求解仿射（与 HALCON vector_to_hom_mat2d 同语义，
        /// P7 纯 Core 实现，不依赖 Runtime）。至少 3 对；共线/退化点集抛异常。
        /// </summary>
        public static AffineTransform2D FromPointPairs(
            System.Collections.Generic.IReadOnlyList<Geometry.Point2D> from,
            System.Collections.Generic.IReadOnlyList<Geometry.Point2D> to)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            if (from.Count != to.Count)
                throw new ArgumentException($"点对数量不一致：{from.Count} vs {to.Count}。");
            if (from.Count < 3)
                throw new ArgumentException($"仿射求解至少需要 3 对点，实际 {from.Count} 对。");

            // 未知数 [a b tx c d ty]；每对贡献两行；解正规方程 M·p = rhs（6×6 高斯消元）。
            var m = new double[6, 6];
            var rhs = new double[6];
            for (var i = 0; i < from.Count; i++)
            {
                var x = from[i].X;
                var y = from[i].Y;
                var u = to[i].X;
                var v = to[i].Y;
                var rows = new double[2][]
                {
                    new double[6] { x, y, 1, 0, 0, 0 },
                    new double[6] { 0, 0, 0, x, y, 1 },
                };
                var targets = new double[2] { u, v };
                for (var r = 0; r < 2; r++)
                {
                    for (var a = 0; a < 6; a++)
                    {
                        rhs[a] += rows[r][a] * targets[r];
                        for (var b = 0; b < 6; b++)
                            m[a, b] += rows[r][a] * rows[r][b];
                    }
                }
            }

            var p = SolveSymmetric(m, rhs);
            return new AffineTransform2D(p[0], p[1], p[2], p[3], p[4], p[5]);
        }

        /// <summary>
        /// 映射轴对齐框：中心精确映射；宽高按列模缩放（相似变换下精确，
        /// 否则为近似，调用方须知晓——Tool 结果转 world 框用此口径）。
        /// </summary>
        public Geometry.Rect2D MapBox(Geometry.Rect2D box)
        {
            var center = Transform(box.Center);
            var sx = Math.Sqrt(A * A + C * C);
            var sy = Math.Sqrt(B * B + D * D);
            return new Geometry.Rect2D(
                center.X - box.Width * sx * 0.5,
                center.Y - box.Height * sy * 0.5,
                box.Width * sx,
                box.Height * sy);
        }

        /// <summary>
        /// 由 HALCON 风格矩阵串解析（行主序 "a,b,c|d,e,f|0,0,1"；兼容逗号/空格写法）。
        /// 末行必须为 0,0,1（否则不是仿射，抛异常）。
        /// </summary>
        public static AffineTransform2D FromHalconMatrixString(string matrix)
        {
            if (string.IsNullOrWhiteSpace(matrix))
                throw new ArgumentException("矩阵串为空。", nameof(matrix));
            var parts = matrix.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 9)
                throw new FormatException($"矩阵串应含 9 个数字，实际 {parts.Length} 个：{matrix}");
            var values = new double[9];
            for (var i = 0; i < 9; i++)
            {
                if (!double.TryParse(parts[i].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out values[i]))
                    throw new FormatException($"矩阵串第 {i + 1} 个数字无法解析：{parts[i]}");
            }

            if (Math.Abs(values[6]) > 1e-9 || Math.Abs(values[7]) > 1e-9 || Math.Abs(values[8] - 1) > 1e-9)
                throw new FormatException($"末行非 0,0,1，不是二维仿射：{matrix}");
            return new AffineTransform2D(values[0], values[1], values[2], values[3], values[4], values[5]);
        }

        /// <summary>格式化为 HALCON 风格矩阵串（3 位小数，供 Tools.Halcon 互操作）。</summary>
        public string ToMatrixString()
        {
            static string F(double v) => (v + 0.0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            return $"{F(A)},{F(B)},{F(Tx)}|{F(C)},{F(D)},{F(Ty)}|{F(0)},{F(0)},{F(1)}";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SolveSymmetric。
        /// 正文中的关键协作调用包括 Abs；具体条件和先后次序见方法体。
        /// </summary>
        private static double[] SolveSymmetric(double[,] m, double[] rhs)
        {
            const int n = 6;
            var a = new double[n, n + 1];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                    a[i, j] = m[i, j];
                a[i, n] = rhs[i];
            }

            for (var col = 0; col < n; col++)
            {
                var pivot = col;
                for (var row = col + 1; row < n; row++)
                {
                    if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                        pivot = row;
                }

                if (Math.Abs(a[pivot, col]) <= 1e-12)
                    throw new InvalidOperationException("点集退化（共线或重合），无法求解仿射。");
                if (pivot != col)
                {
                    for (var k = col; k <= n; k++)
                        (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                }

                for (var row = 0; row < n; row++)
                {
                    if (row == col) continue;
                    var factor = a[row, col] / a[col, col];
                    for (var k = col; k <= n; k++)
                        a[row, k] -= factor * a[col, k];
                }
            }

            var result = new double[n];
            for (var i = 0; i < n; i++)
                result[i] = a[i, n] / a[i, i];
            return result;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Equals。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </remarks>
        public bool Equals(AffineTransform2D other) =>
            A.Equals(other.A) && B.Equals(other.B) && Tx.Equals(other.Tx) &&
            C.Equals(other.C) && D.Equals(other.D) && Ty.Equals(other.Ty);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Equals。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </remarks>
        public override bool Equals(object? obj) => obj is AffineTransform2D other && Equals(other);

        /// <inheritdoc/>
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetHashCode。
        /// 正文中的关键协作调用包括 Combine；具体条件和先后次序见方法体。
        /// </remarks>
        public override int GetHashCode() => HashCode.Combine(A, B, Tx, C, D, Ty);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() => $"[{A:0.###} {B:0.###} {Tx:0.###}; {C:0.###} {D:0.###} {Ty:0.###}]";

        /// <summary>值相等。</summary>
        public static bool operator ==(AffineTransform2D left, AffineTransform2D right) => left.Equals(right);

        /// <summary>值不等。</summary>
        public static bool operator !=(AffineTransform2D left, AffineTransform2D right) => !left.Equals(right);
    }
}
