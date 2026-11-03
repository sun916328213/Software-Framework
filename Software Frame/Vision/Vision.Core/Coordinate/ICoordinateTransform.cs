using System.Text.Json.Serialization;

namespace Vision.Core.Coordinate
{
    /// <summary>
    /// 坐标空间分类，用于说明图像、定位、标定和机械坐标之间的关系。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CoordinateSpace
    {
        /// <summary>
        /// 未指定。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 图像像素坐标（左上为原点，Y 轴朝下）。
        /// </summary>
        Image,

        /// <summary>
        /// 定位/夹具坐标系（Fixture 结果建立，随工件姿态变化）。
        /// </summary>
        Fixture,

        /// <summary>
        /// 标定后的物理坐标系（标定结果建立，通常是毫米）。
        /// </summary>
        Calibration,

        /// <summary>
        /// 机台/机械坐标系（与运动控制对齐，做手眼换算后的落点）。
        /// </summary>
        Machine,

        /// <summary>
        /// 工具内部坐标系（某次工具运行的局部参考，如模板匹配的原点）。
        /// </summary>
        Tool,
    }

    /// <summary>
    /// 一次坐标系变换的契约。
    ///
    /// 接口只约定正向与反向映射，不要求变换一定为刚体。
    /// 使用位姿、仿射或其他映射的能力由具体实现明确说明。
    /// </summary>
    public interface ICoordinateTransform
    {
        /// <summary>
        /// 源坐标系。
        /// </summary>
        CoordinateSpace Source
        {
            get;
        }

        /// <summary>
        /// 目标坐标系。
        /// </summary>
        CoordinateSpace Target
        {
            get;
        }

        /// <summary>
        /// 是否可逆（<see cref="TransformInverse"/> 是否有效）。
        /// </summary>
        bool IsInvertible
        {
            get;
        }

        /// <summary>
        /// 把点从 <see cref="Source"/> 映射到 <see cref="Target"/>。
        /// </summary>
        /// <param name="point">源坐标系下的点。</param>
        /// <returns>目标坐标系下的点。</returns>
        Geometry.Point2D Transform(Geometry.Point2D point);

        /// <summary>
        /// 把点从 <see cref="Target"/> 反映射回 <see cref="Source"/>。
        /// </summary>
        /// <param name="point">目标坐标系下的点。</param>
        /// <returns>源坐标系下的点。</returns>
        /// <exception cref="InvalidOperationException"><see cref="IsInvertible"/> 为 false 时抛出。</exception>
        Geometry.Point2D TransformInverse(Geometry.Point2D point);
    }
}
