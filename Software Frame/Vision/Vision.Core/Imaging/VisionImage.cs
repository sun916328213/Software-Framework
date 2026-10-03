// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：PixelFormat、VisionImage。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.Text.Json.Serialization;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Imaging」）：
//   计划原文：「公共参数和结果使用稳定领域类型；避免直接返回 HTuple/HObject/Mat。」
//   本类型就是那个「稳定领域类型」—— 只描述像素缓冲本身，**不认识任何厂商 SDK**。
//   ⛔ 不要在这里加 HObject / Mat / Bitmap 字段或转换方法：那是 Vision.Tools.* / Vision.AI.*
//      （P3/P4/P5）的职责，转换器也放那边。Vision.Core 必须能脱离所有视觉 SDK 独立编译。
//   现状：P2 只落契约，尚无生产者（相机采集、文件加载都在后续阶段接）。
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
        /// <summary>未指定。默认值，读到它说明图像还没被正确初始化。</summary>
        Unknown = 0,

        /// <summary>8 位单通道灰度。</summary>
        Mono8,

        /// <summary>16 位单通道灰度（小端）。</summary>
        Mono16,

        /// <summary>24 位 RGB，通道顺序 R-G-B。</summary>
        Rgb24,

        /// <summary>24 位 BGR，通道顺序 B-G-R（OpenCV 原生顺序）。</summary>
        Bgr24,

        /// <summary>32 位 BGRA，通道顺序 B-G-R-A。</summary>
        Bgra32,

        /// <summary>32 位单通道浮点。</summary>
        Float32,
    }

    /// <summary>
    /// 一帧图像 —— 视觉域里「被处理的输入」的统一表示。
    ///
    /// 设计取向：
    /// <list type="bullet">
    ///   <item>像素以 <see cref="byte"/> 缓冲承载（<see cref="Pixels"/>），
    ///     <see cref="Mono16"/>/<see cref="Float32"/> 时按小端重新解释，不做拆箱；
    ///     这样 P3 的 HALCON 互操作可以零拷贝拿到指针（<c>HImage</c> 由 <c>byte[]</c> 构造）。</item>
    ///   <item>属性全部用公有 <c>set</c> —— 与仓库既有约定一致（<c>System.Text.Json</c>
    ///     对非公有 setter 是**静默跳过**，且属性面板也据此判可编辑性；
    ///     详见 <c>WPFControls.Tests/SerializationContractTests</c>）。</item>
    ///   <item>只读派生量（<see cref="IsEmpty"/> 等）标 <c>[JsonIgnore]</c>：
    ///     STJ 本来就能写、但不能读，留着只会往存盘文件里灌冗余字段。</item>
    /// </list>
    /// </summary>
    public sealed class VisionImage
    {
        /// <summary>宽度（像素）。</summary>
        public int Width { get; set; }

        /// <summary>高度（像素）。</summary>
        public int Height { get; set; }

        /// <summary>
        /// 一行像素占用的字节数。0 表示按 <see cref="ExpectedStride"/> 紧凑排布
        /// （相机常有行对齐填充，此时必须显式给值）。
        /// </summary>
        public int Stride { get; set; }

        /// <summary>像素格式。</summary>
        public PixelFormat Format { get; set; } = PixelFormat.Unknown;

        /// <summary>像素缓冲。长度应不小于 <see cref="ExpectedStride"/> × <see cref="Height"/>。</summary>
        public byte[] Pixels { get; set; }

        /// <summary>帧序号（相机侧的递增计数；文件加载场景可留 0）。</summary>
        public long FrameId { get; set; }

        /// <summary>采集/加载时刻。</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>来源标识（相机名 / 文件路径 / "Synthetic"）。仅用于诊断与追溯。</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>没有像素（未初始化或已被清空）。</summary>
        [JsonIgnore]
        public bool IsEmpty => Width <= 0 || Height <= 0 || Pixels == null || Pixels.Length == 0;

        /// <summary>单像素字节数；<see cref="PixelFormat.Unknown"/> 时为 0。</summary>
        [JsonIgnore]
        public int BytesPerPixel => Format switch
        {
            PixelFormat.Mono8 => 1,
            PixelFormat.Mono16 => 2,
            PixelFormat.Float32 => 4,
            PixelFormat.Rgb24 => 3,
            PixelFormat.Bgr24 => 3,
            PixelFormat.Bgra32 => 4,
            _ => 0,
        };

        /// <summary>紧凑排布（无行填充）时一行应有的字节数；<see cref="IsEmpty"/> 或格式未知时为 0。</summary>
        [JsonIgnore]
        public int ExpectedStride => IsEmpty ? 0 : Width * BytesPerPixel;

        /// <summary>
        /// 用一块现成缓冲构造图像。只做一致性校验，**不拷贝**像素。
        /// </summary>
        /// <param name="width">宽度（像素），必须为正。</param>
        /// <param name="height">高度（像素），必须为正。</param>
        /// <param name="format">像素格式，不能是 <see cref="PixelFormat.Unknown"/>。</param>
        /// <param name="pixels">像素缓冲。</param>
        /// <param name="stride">行字节数；&lt;= 0 表示紧凑排布。</param>
        /// <param name="source">来源标识。</param>
        /// <returns>构造好的图像。</returns>
        /// <exception cref="ArgumentOutOfRangeException">宽高非正、或格式未知。</exception>
        /// <exception cref="ArgumentException">缓冲长度不足以容纳一行 × 高度。</exception>
        public static VisionImage FromBuffer(
            int width,
            int height,
            PixelFormat format,
            byte[] pixels,
            int stride = 0,
            string source = "")
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "宽度必须为正。");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "高度必须为正。");
            if (format == PixelFormat.Unknown)
                throw new ArgumentOutOfRangeException(nameof(format), format, "像素格式不能是 Unknown。");
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));

            var bpp = format switch
            {
                PixelFormat.Mono8 => 1,
                PixelFormat.Mono16 => 2,
                PixelFormat.Float32 => 4,
                PixelFormat.Rgb24 => 3,
                PixelFormat.Bgr24 => 3,
                PixelFormat.Bgra32 => 4,
                _ => 0,
            };

            if (bpp == 0)
                throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的像素格式。");

            var minimumStride = checked(width * bpp);
            if (stride > 0 && stride < minimumStride)
                throw new ArgumentOutOfRangeException(nameof(stride), stride,
                    $"行字节数不能小于一行像素所需的 {minimumStride} 字节。");

            var effectiveStride = stride > 0 ? stride : minimumStride;
            var required = (long)effectiveStride * height;
            if (pixels.LongLength < required)
                throw new ArgumentException(
                    $"像素缓冲长度 {pixels.LongLength} 不足以容纳 {effectiveStride}×{height} = {required} 字节。",
                    nameof(pixels));

            return new VisionImage
            {
                Width = width,
                Height = height,
                Stride = stride,
                Format = format,
                Pixels = pixels,
                Timestamp = DateTime.Now,
                Source = source ?? string.Empty,
            };
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() =>
            IsEmpty
                ? "VisionImage(empty)"
                : $"VisionImage({Width}×{Height}, {Format}, stride={Stride}, frame={FrameId}, src={Source})";
    }
}
