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
        public bool Equals(Size2D other) => Width.Equals(other.Width) && Height.Equals(other.Height);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Size2D other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => (Width.GetHashCode() * 397) ^ Height.GetHashCode();

        /// <inheritdoc/>
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
