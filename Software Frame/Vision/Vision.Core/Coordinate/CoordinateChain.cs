// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：RigidTransform、AffineTransform、CoordinateChain。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// ─────────────────────────────────────────────────────────────────────────────
// P7 —— ICoordinateTransform 的两种实现 + 标准坐标链
// （计划 §P7「建立 Image -> Fixture -> Calibration -> Machine 坐标链」；
// P2 在 ICoordinateTransform 注释里预告的“可逆刚体便捷实现”即 RigidTransform）。
//
// 链的装配语义（务必先读再改）：
//   · Link1 Image→Fixture：fixture 姿态 F 以“fixture→image”给出（FixtureTool 的产出），
//     正向（像素→fixture）用其逆；
//   · Link2 Fixture→Calibration：标定仿射（像素当量/轴不正交；N 点最小二乘见
//     AffineTransform2D.FromPointPairs，与 HALCON vector_to_hom_mat2d 同语义）；
//   · Link3 Calibration→Machine：机台安装位姿（world→machine，刚体）。
// 链只走正/反两个方向；中间系的点用 Transform 分段取（见 PixelToWorld）。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Core.Coordinate
{
    /// <summary>
    /// 刚体变换实现（Pose2D 承载；恒可逆）。
    /// <paramref name="pose"/> 把 <paramref name="source"/> 系的点映射到
    /// <paramref name="target"/> 系（先旋转再平移，与 Pose2D 约定一致）。
    /// </summary>
    public sealed class RigidTransform : ICoordinateTransform
    {
        /// <summary>构造一个刚体变换。</summary>
        public RigidTransform(CoordinateSpace source, CoordinateSpace target, Geometry.Pose2D pose)
        {
            Source = source;
            Target = target;
            Pose = pose;
        }

        /// <inheritdoc/>
        public CoordinateSpace Source { get; set; }

        /// <inheritdoc/>
        public CoordinateSpace Target { get; set; }

        /// <summary>刚体位姿（源→目标）。</summary>
        public Geometry.Pose2D Pose { get; set; }

        /// <inheritdoc/>
        public bool IsInvertible => true;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Transform。
        /// 正文中的关键协作调用包括 Transform；具体条件和先后次序见方法体。
        /// </remarks>
        public Geometry.Point2D Transform(Geometry.Point2D point) => Pose.Transform(point);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 TransformInverse。
        /// 正文中的关键协作调用包括 InverseTransform；具体条件和先后次序见方法体。
        /// </remarks>
        public Geometry.Point2D TransformInverse(Geometry.Point2D point) => Pose.InverseTransform(point);
    }

    /// <summary>
    /// 仿射变换实现（标定链；奇异时反向抛异常）。
    /// </summary>
    public sealed class AffineTransform : ICoordinateTransform
    {
        /// <summary>构造一个仿射变换。</summary>
        public AffineTransform(CoordinateSpace source, CoordinateSpace target, AffineTransform2D affine)
        {
            Source = source;
            Target = target;
            Affine = affine;
        }

        /// <inheritdoc/>
        public CoordinateSpace Source { get; set; }

        /// <inheritdoc/>
        public CoordinateSpace Target { get; set; }

        /// <summary>仿射（源→目标）。</summary>
        public AffineTransform2D Affine { get; set; }

        /// <inheritdoc/>
        public bool IsInvertible => Affine.IsInvertible;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Transform。
        /// 正文中的关键协作调用包括 Transform；具体条件和先后次序见方法体。
        /// </remarks>
        public Geometry.Point2D Transform(Geometry.Point2D point) => Affine.Transform(point);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 TransformInverse。
        /// 正文中的关键协作调用包括 TransformInverse；具体条件和先后次序见方法体。
        /// </remarks>
        public Geometry.Point2D TransformInverse(Geometry.Point2D point) => Affine.TransformInverse(point);
    }

    /// <summary>
    /// 标准坐标链：Image → Fixture → Calibration → Machine。
    /// 构造时校验首尾衔接；映射按序逐段施加。
    /// </summary>
    public sealed class CoordinateChain
    {
        /// <summary>用三段标准环节装配（见文件头语义说明）。</summary>
        /// <param name="fixtureInImage">fixture 在图像中的位姿（fixture→image）。</param>
        /// <param name="fixtureToWorld">fixture→world 标定仿射。</param>
        /// <param name="worldToMachine">world→machine 安装位姿。</param>
        public CoordinateChain(
            Geometry.Pose2D fixtureInImage,
            AffineTransform2D fixtureToWorld,
            Geometry.Pose2D worldToMachine)
        {
            Links = new System.Collections.Generic.List<ICoordinateTransform>
            {
                new RigidTransform(CoordinateSpace.Image, CoordinateSpace.Fixture, fixtureInImage.Inverse()),
                new AffineTransform(CoordinateSpace.Fixture, CoordinateSpace.Calibration, fixtureToWorld),
                new RigidTransform(CoordinateSpace.Calibration, CoordinateSpace.Machine, worldToMachine),
            };
        }

        /// <summary>由调用方给定的环节装配（必须首尾衔接且至少一段）。</summary>
        public CoordinateChain(System.Collections.Generic.IReadOnlyList<ICoordinateTransform> links)
        {
            ArgumentNullException.ThrowIfNull(links);
            if (links.Count == 0)
                throw new ArgumentException("坐标链至少需要一段变换。", nameof(links));
            for (var i = 0; i < links.Count - 1; i++)
            {
                if (links[i] == null || links[i + 1] == null)
                    throw new ArgumentException($"第 {i + 1} 段变换为 null。", nameof(links));
                if (links[i].Target != links[i + 1].Source)
                    throw new ArgumentException(
                        $"第 {i + 1} 段目标 {links[i].Target} 与第 {i + 2} 段源 {links[i + 1].Source} 不衔接。",
                        nameof(links));
            }

            Links = links;
        }

        /// <summary>各段变换（源→目标有序）。</summary>
        public System.Collections.Generic.IReadOnlyList<ICoordinateTransform> Links { get; }

        /// <summary>链源坐标系。</summary>
        public CoordinateSpace Source => Links[0].Source;

        /// <summary>链目标坐标系。</summary>
        public CoordinateSpace Target => Links[Links.Count - 1].Target;

        /// <summary>正向映射（源→目标）。</summary>
        public Geometry.Point2D Transform(Geometry.Point2D point)
        {
            var current = point;
            foreach (var link in Links)
                current = link.Transform(current);
            return current;
        }

        /// <summary>反映射（目标→源）；任一段不可逆时抛异常。</summary>
        public Geometry.Point2D TransformInverse(Geometry.Point2D point)
        {
            var current = point;
            for (var i = Links.Count - 1; i >= 0; i--)
                current = Links[i].TransformInverse(current);
            return current;
        }

        /// <summary>像素→world（前两段；标准链专用）。</summary>
        public Geometry.Point2D PixelToWorld(Geometry.Point2D pixel) =>
            Links[1].Transform(Links[0].Transform(pixel));

        /// <summary>像素→机台（整链；标准链专用）。</summary>
        public Geometry.Point2D PixelToMachine(Geometry.Point2D pixel) => Transform(pixel);

        /// <summary>world→像素（前两段逆；标准链专用）。</summary>
        public Geometry.Point2D WorldToPixel(Geometry.Point2D world) =>
            Links[0].TransformInverse(Links[1].TransformInverse(world));

        /// <summary>
        /// 整链合成一个仿射（供框/ROI 批量映射；刚体环节经 FromPose 转仿射）。
        /// </summary>
        public AffineTransform2D ToAffine() => ToAffine(Links.Count);

        /// <summary>
        /// 前 <paramref name="linkCount"/> 段合成一个仿射（1-based 计数；
        /// world 系映射用前两段，机台用整链）。
        /// </summary>
        public AffineTransform2D ToAffine(int linkCount)
        {
            if (linkCount < 1 || linkCount > Links.Count)
                throw new ArgumentOutOfRangeException(nameof(linkCount), $"须在 1..{Links.Count} 之间。");
            var total = AffineTransform2D.Identity;
            for (var i = 0; i < linkCount; i++)
            {
                var link = Links[i];
                var affine = link switch
                {
                    AffineTransform a => a.Affine,
                    RigidTransform r => AffineTransform2D.FromPose(r.Pose),
                    _ => throw new NotSupportedException($"不支持的环节类型 {link.GetType().Name}，无法合成仿射。"),
                };
                total = affine.Compose(total);
            }

            return total;
        }
    }
}
