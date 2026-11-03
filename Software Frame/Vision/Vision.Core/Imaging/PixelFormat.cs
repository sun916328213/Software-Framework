using System.Text.Json.Serialization;

namespace Vision.Core.Imaging
{
    /// <summary>
    /// 像素格式。
    /// ⛔ 刻意只用「通道数 + 每通道位宽」表达，不出现任何厂商枚举（HALCON 的 <c>HImageType</c>、
    /// OpenCV 的 <c>MatType</c>）—— 由各 Backend 在边界处做映射。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PixelFormat
    {
        /// <summary>
        /// 未指定。默认值，读到它说明图像还没被正确初始化。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 8 位单通道灰度。
        /// </summary>
        Mono8,

        /// <summary>
        /// 16 位单通道灰度（小端）。
        /// </summary>
        Mono16,

        /// <summary>
        /// 24 位 RGB，通道顺序 R-G-B。
        /// </summary>
        Rgb24,

        /// <summary>
        /// 24 位 BGR，通道顺序 B-G-R（OpenCV 原生顺序）。
        /// </summary>
        Bgr24,

        /// <summary>
        /// 32 位 BGRA，通道顺序 B-G-R-A。
        /// </summary>
        Bgra32,

        /// <summary>
        /// 32 位单通道浮点。
        /// </summary>
        Float32,
    }

}
