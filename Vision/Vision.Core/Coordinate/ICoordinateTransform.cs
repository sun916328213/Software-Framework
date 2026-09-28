using System.Text.Json.Serialization;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Coordinate」）。
//   §P7 要求「建立 Image -> Fixture -> Calibration -> Machine 坐标链」。
//   P2 先把「有哪些坐标系」和「一次变换长什么样」定下来，链的组装与实现归 P7。
namespace Vision.Core.Coordinate
{
    /// <summary>
    /// 坐标系标识。按计划 §P7 的链路顺序排列：Image → Fixture → Calibration → Machine。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CoordinateSpace
    {
        /// <summary>未指定。</summary>
        Unknown = 0,

        /// <summary>图像像素坐标（左上为原点，Y 轴朝下）。</summary>
        Image,

        /// <summary>定位/夹具坐标系（Fixture 结果建立，随工件姿态变化）。</summary>
        Fixture,

        /// <summary>标定后的物理坐标系（标定结果建立，通常是毫米）。</summary>
        Calibration,

        /// <summary>机台/机械坐标系（与运动控制对齐，做手眼换算后的落点）。</summary>
        Machine,

        /// <summary>工具内部坐标系（某次工具运行的局部参考，如模板匹配的原点）。</summary>
        Tool,
    }

    /// <summary>
    /// 一次坐标系变换的契约。
    ///
    /// ⛔ 刻意<b>不</b>要求实现必须是刚体变换：标定链里可能有镜头畸变校正、仿射甚至透视，
    /// 那些无法用单个 <see cref="Geometry.Pose2D"/> 表达。所以这里只暴露「正向/反向映射」，
    /// 需要位姿的地方另行判断（P7 会给可逆刚体变换补一个 <c>Pose</c> 便捷实现）。
    /// </summary>
    public interface ICoordinateTransform
    {
        /// <summary>源坐标系。</summary>
        CoordinateSpace Source { get; }

        /// <summary>目标坐标系。</summary>
        CoordinateSpace Target { get; }

        /// <summary>是否可逆（<see cref="TransformInverse"/> 是否有效）。</summary>
        bool IsInvertible { get; }

        /// <summary>把点从 <see cref="Source"/> 映射到 <see cref="Target"/>。</summary>
        /// <param name="point">源坐标系下的点。</param>
        /// <returns>目标坐标系下的点。</returns>
        Geometry.Point2D Transform(Geometry.Point2D point);

        /// <summary>把点从 <see cref="Target"/> 反映射回 <see cref="Source"/>。</summary>
        /// <param name="point">目标坐标系下的点。</param>
        /// <returns>源坐标系下的点。</returns>
        /// <exception cref="InvalidOperationException"><see cref="IsInvertible"/> 为 false 时抛出。</exception>
        Geometry.Point2D TransformInverse(Geometry.Point2D point);
    }
}
