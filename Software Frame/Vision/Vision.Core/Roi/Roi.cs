// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：Roi。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// ─────────────────────────────────────────────────────────────────────────────
// P7 —— ROI 实体（计划 §P7「ROI 可跟随 Fixture」）。
//
// 沿用 P2 在 IRoi 注释里定的统一表达：Pose + Size 描述几何，Polygon 另加顶点。
// 跟随语义（刚体精确）：训练时 fixture 为 F0，ROI 位姿 P（roi→image）；
// 运行时 fixture 为 F1，ROI 新位姿 = (F1∘F0⁻¹)∘P。多边形顶点同运动映射。
// 一般仿射映射（标定系之间）：相似变换保持形状（角度吸收旋转）；非相似时
// 矩形退化为多边形（诚实表达，不伪装成轴对齐框），圆退化为椭圆近似。
// Mask 只有位姿跟随（掩膜载荷由后端持有，见 RoiKind 注释）。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Core.Roi
{
    /// <summary>
    /// ROI 实体（IRoi 的可序列化实现；全部公有 setter，JSON 可往返）。
    /// </summary>
    public sealed class Roi : IRoi
    {
        /// <inheritdoc/>
        public RoiKind Kind { get; set; } = RoiKind.Unknown;

        /// <inheritdoc/>
        public Geometry.Pose2D Pose { get; set; }

        /// <inheritdoc/>
        public Geometry.Size2D Size { get; set; }

        /// <summary>多边形顶点（图像坐标系；仅 Polygon 使用）。</summary>
        public System.Collections.Generic.List<Geometry.Point2D> OutlinePoints { get; set; } = new();

        /// <inheritdoc/>
        public Geometry.Rect2D Bounds => ComputeBounds();

        /// <inheritdoc/>
        public System.Collections.Generic.IReadOnlyList<Geometry.Point2D> Outline => ComputeOutline();

        /// <inheritdoc/>
        public bool IsEmpty =>
            Kind == RoiKind.Unknown || Size.Width <= 0 || Size.Height <= 0 ||
            (Kind == RoiKind.Polygon && OutlinePoints.Count == 0);

        /// <summary>轴对齐矩形（中心 + 宽高）。</summary>
        public static Roi Rectangle(double centerX, double centerY, double width, double height) => new()
        {
            Kind = RoiKind.Rectangle,
            Pose = new Geometry.Pose2D(centerX, centerY, 0),
            Size = new Geometry.Size2D(width, height),
        };

        /// <summary>带角度矩形（中心 + 宽高 + 角度/度）。</summary>
        public static Roi RotatedRectangle(double centerX, double centerY, double width, double height, double angleDeg) => new()
        {
            Kind = RoiKind.RotatedRectangle,
            Pose = new Geometry.Pose2D(centerX, centerY, angleDeg),
            Size = new Geometry.Size2D(width, height),
        };

        /// <summary>圆（圆心 + 直径）。</summary>
        public static Roi Circle(double centerX, double centerY, double diameter) => new()
        {
            Kind = RoiKind.Circle,
            Pose = new Geometry.Pose2D(centerX, centerY, 0),
            Size = new Geometry.Size2D(diameter, diameter),
        };

        /// <summary>椭圆（圆心 + 两直径 + 角度/度）。</summary>
        public static Roi Ellipse(double centerX, double centerY, double diameterX, double diameterY, double angleDeg) => new()
        {
            Kind = RoiKind.Ellipse,
            Pose = new Geometry.Pose2D(centerX, centerY, angleDeg),
            Size = new Geometry.Size2D(diameterX, diameterY),
        };

        /// <summary>多边形（顶点；Pose/Size 置为其外接框，符合 IRoi 约定）。</summary>
        public static Roi Polygon(System.Collections.Generic.IReadOnlyList<Geometry.Point2D> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            var roi = new Roi { Kind = RoiKind.Polygon };
            roi.OutlinePoints.AddRange(points);
            roi.RefreshPolygonFrame();
            return roi;
        }

        /// <summary>
        /// 跟随 Fixture（刚体精确）：F0 为训练时 fixture 位姿（fixture→image），
        /// F1 为运行时 fixture 位姿；ROI 按 fixture 运动量整体搬移。
        /// </summary>
        public Roi FollowFixture(Geometry.Pose2D oldFixture, Geometry.Pose2D newFixture)
        {
            var motion = newFixture.Compose(oldFixture.Inverse());
            return TransformRigid(motion);
        }

        /// <summary>施加刚体运动（位姿左乘；多边形顶点同映射；尺寸不变）。</summary>
        public Roi TransformRigid(Geometry.Pose2D motion)
        {
            var output = CloneShallow();
            output.Pose = motion.Compose(Pose);
            if (output.Kind == RoiKind.Rectangle &&
                Math.Abs(Geometry.Pose2D.NormalizeAngle(output.Pose.AngleDeg)) > 1e-9)
            {
                // 旋转后的矩形不再是轴对齐矩形，诚实改类（IRoi 约定 Rectangle 角度为 0）。
                output.Kind = RoiKind.RotatedRectangle;
            }

            if (output.Kind == RoiKind.Circle)
            {
                // 圆绕任意点旋转后仍是圆，角度无意义，清零以保持约定干净。
                output.Pose = new Geometry.Pose2D(output.Pose.X, output.Pose.Y, 0);
            }

            if (Kind == RoiKind.Polygon)
            {
                output.OutlinePoints = new System.Collections.Generic.List<Geometry.Point2D>(OutlinePoints.Count);
                foreach (var point in OutlinePoints)
                    output.OutlinePoints.Add(motion.Transform(point));
                output.RefreshPolygonFrame();
            }

            return output;
        }

        /// <summary>
        /// 施加一般仿射（标定系之间映射）。相似变换保持形状；非相似时矩形→多边形、
        /// 圆→椭圆近似（见文件头）；Mask 只跟随位姿中心。
        /// </summary>
        public Roi Transform(Coordinate.AffineTransform2D map)
        {
            var center = map.Transform(new Geometry.Point2D(Pose.X, Pose.Y));
            if (Kind == RoiKind.Polygon)
            {
                var mapped = new System.Collections.Generic.List<Geometry.Point2D>(OutlinePoints.Count);
                foreach (var point in OutlinePoints)
                    mapped.Add(map.Transform(point));
                return Polygon(mapped);
            }

            if (Kind == RoiKind.Mask)
            {
                var output = CloneShallow();
                output.Pose = new Geometry.Pose2D(center.X, center.Y, Pose.AngleDeg);
                return output;
            }

            if (map.IsSimilarity(1e-9, out var scale, out var mapAngle))
            {
                var size = new Geometry.Size2D(Size.Width * scale, Size.Height * scale);
                var angle = Geometry.Pose2D.NormalizeAngle(Pose.AngleDeg + mapAngle);
                return Kind switch
                {
                    RoiKind.Rectangle when Math.Abs(angle) <= 1e-9 => new Roi
                    {
                        Kind = RoiKind.Rectangle,
                        Pose = new Geometry.Pose2D(center.X, center.Y, 0),
                        Size = size,
                    },
                    RoiKind.Rectangle => RotatedRectangle(center.X, center.Y, size.Width, size.Height, angle),
                    RoiKind.Circle => Circle(center.X, center.Y, Size.Width * scale),
                    _ => new Roi
                    {
                        Kind = Kind,
                        Pose = new Geometry.Pose2D(center.X, center.Y, angle),
                        Size = size,
                    },
                };
            }

            // 非相似：矩形类退化为多边形；圆/椭圆用列模缩放 + 主轴角近似为椭圆。
            if (Kind == RoiKind.Rectangle || Kind == RoiKind.RotatedRectangle)
            {
                var corners = RectCorners();
                var mapped = new System.Collections.Generic.List<Geometry.Point2D>(corners.Count);
                foreach (var corner in corners)
                    mapped.Add(map.Transform(corner));
                return Polygon(mapped);
            }

            var sx = Math.Sqrt(map.A * map.A + map.C * map.C);
            var sy = Math.Sqrt(map.B * map.B + map.D * map.D);
            var axisAngle = Math.Atan2(map.C, map.A) * 180.0 / Math.PI;
            return Ellipse(center.X, center.Y, Size.Width * sx, Size.Height * sy,
                Geometry.Pose2D.NormalizeAngle(Pose.AngleDeg + axisAngle));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CloneShallow。
        /// </summary>
        private Roi CloneShallow() => new()
        {
            Kind = Kind,
            Pose = Pose,
            Size = Size,
            OutlinePoints = new System.Collections.Generic.List<Geometry.Point2D>(OutlinePoints),
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RefreshPolygonFrame。
        /// </summary>
        private void RefreshPolygonFrame()
        {
            if (OutlinePoints.Count == 0)
            {
                Pose = default;
                Size = default;
                return;
            }

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;
            foreach (var point in OutlinePoints)
            {
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }

            Pose = new Geometry.Pose2D((minX + maxX) * 0.5, (minY + maxY) * 0.5, 0);
            Size = new Geometry.Size2D(maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RectCorners。
        /// 正文中的关键协作调用包括 Add、Transform；具体条件和先后次序见方法体。
        /// </summary>
        private System.Collections.Generic.List<Geometry.Point2D> RectCorners()
        {
            var hw = Size.Width * 0.5;
            var hh = Size.Height * 0.5;
            var corners = new System.Collections.Generic.List<Geometry.Point2D>(4)
            {
                new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh),
            };
            var output = new System.Collections.Generic.List<Geometry.Point2D>(4);
            foreach (var corner in corners)
                output.Add(Pose.Transform(corner));
            return output;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ComputeBounds。
        /// 正文中的关键协作调用包括 RectCorners、Cos、Sin、Sqrt；具体条件和先后次序见方法体。
        /// </summary>
        private Geometry.Rect2D ComputeBounds()
        {
            switch (Kind)
            {
                case RoiKind.Rectangle:
                case RoiKind.RotatedRectangle:
                case RoiKind.Polygon:
                {
                    var points = Kind == RoiKind.Polygon
                        ? OutlinePoints
                        : (System.Collections.Generic.IReadOnlyList<Geometry.Point2D>)RectCorners();
                    if (points.Count == 0)
                        return Geometry.Rect2D.Empty;
                    var minX = double.MaxValue;
                    var minY = double.MaxValue;
                    var maxX = double.MinValue;
                    var maxY = double.MinValue;
                    foreach (var point in points)
                    {
                        if (point.X < minX) minX = point.X;
                        if (point.Y < minY) minY = point.Y;
                        if (point.X > maxX) maxX = point.X;
                        if (point.Y > maxY) maxY = point.Y;
                    }

                    return new Geometry.Rect2D(minX, minY, maxX - minX, maxY - minY);
                }

                case RoiKind.Circle:
                {
                    var r = Size.Width * 0.5;
                    return new Geometry.Rect2D(Pose.X - r, Pose.Y - r, r * 2, r * 2);
                }

                case RoiKind.Ellipse:
                case RoiKind.Mask:
                {
                    // 旋转椭圆 AABB（Mask 按椭圆近似其位姿框）。
                    var a = Size.Width * 0.5;
                    var b = Size.Height * 0.5;
                    var rad = Pose.AngleDeg * Math.PI / 180.0;
                    var cos = Math.Cos(rad);
                    var sin = Math.Sin(rad);
                    var hx = Math.Sqrt(a * a * cos * cos + b * b * sin * sin);
                    var hy = Math.Sqrt(a * a * sin * sin + b * b * cos * cos);
                    return new Geometry.Rect2D(Pose.X - hx, Pose.Y - hy, hx * 2, hy * 2);
                }

                default:
                    return Geometry.Rect2D.Empty;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ComputeOutline。
        /// 正文中的关键协作调用包括 RectCorners、Add、Transform、Cos、Sin；具体条件和先后次序见方法体。
        /// </summary>
        private System.Collections.Generic.List<Geometry.Point2D> ComputeOutline()
        {
            switch (Kind)
            {
                case RoiKind.Rectangle:
                case RoiKind.RotatedRectangle:
                    return RectCorners();
                case RoiKind.Polygon:
                    return new System.Collections.Generic.List<Geometry.Point2D>(OutlinePoints);
                case RoiKind.Circle:
                case RoiKind.Ellipse:
                case RoiKind.Mask:
                {
                    // 采样闭合轮廓（显示与粗判用，不用于精确测量）。
                    var points = new System.Collections.Generic.List<Geometry.Point2D>(24);
                    for (var i = 0; i < 24; i++)
                    {
                        var angle = i * 2 * Math.PI / 24;
                        points.Add(Pose.Transform(new Geometry.Point2D(
                            Math.Cos(angle) * Size.Width * 0.5,
                            Math.Sin(angle) * Size.Height * 0.5)));
                    }

                    return points;
                }

                default:
                    return new System.Collections.Generic.List<Geometry.Point2D>();
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() => $"{Kind} pose={Pose} size={Size}";
    }
}
