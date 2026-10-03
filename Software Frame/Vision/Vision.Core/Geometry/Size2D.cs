// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：Size2D。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Geometry」）。
namespace Vision.Core.Geometry
{
    /// <summary>
    /// 二维尺寸（宽 × 高）。
    /// 与 <see cref="Rect2D"/> 分开：ROI / 模板 / 卷积核都只需要尺寸，不需要位置。
    /// </summary>
    public struct Size2D : IEquatable<Size2D>
    {
        /// <summary>宽。</summary>
        public double Width { get; set; }

        /// <summary>高。</summary>
        public double Height { get; set; }

        /// <summary>构造一个尺寸。</summary>
        /// <param name="width">宽。</param>
        /// <param name="height">高。</param>
        public Size2D(double width, double height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>空尺寸（默认值）。</summary>
        public static Size2D Empty => default;

        /// <summary>任一维非正即为空。</summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>面积。</summary>
        public double Area => Width * Height;

        /// <summary>对角线长度。</summary>
        public double Diagonal => Math.Sqrt(Width * Width + Height * Height);

        /// <summary>宽高比；高为 0 时返回 0（不抛异常）。</summary>
        public double AspectRatio => Height == 0 ? 0 : Width / Height;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Equals。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </remarks>
        public bool Equals(Size2D other) => Width.Equals(other.Width) && Height.Equals(other.Height);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Equals。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </remarks>
        public override bool Equals(object obj) => obj is Size2D other && Equals(other);

        /// <inheritdoc/>
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetHashCode。
        /// 正文中的关键协作调用包括 GetHashCode；具体条件和先后次序见方法体。
        /// </remarks>
        public override int GetHashCode() => (Width.GetHashCode() * 397) ^ Height.GetHashCode();

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() => $"{Width:0.####}×{Height:0.####}";

        /// <summary>值相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>相等时为 true。</returns>
        public static bool operator ==(Size2D left, Size2D right) => left.Equals(right);

        /// <summary>值不等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>不等时为 true。</returns>
        public static bool operator !=(Size2D left, Size2D right) => !left.Equals(right);
    }
}
