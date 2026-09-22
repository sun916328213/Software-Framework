namespace MotionControl.Definition
{
    /// <summary>
    /// 稳定的运动控制驱动标识。
    /// <para>比较时忽略大小写；第三方驱动可使用反向域名形式，例如 company.motion.driver。</para>
    /// </summary>
    public readonly struct MotionDriverId : IEquatable<MotionDriverId>
    {
        /// <summary>创建并校验驱动标识。</summary>
        public MotionDriverId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("驱动标识不能为空。", nameof(value));

            Value = value.Trim();
        }

        /// <summary>驱动标识文本。</summary>
        public string Value { get; }

        /// <summary>从兼容枚举创建驱动标识。</summary>
        public static MotionDriverId From(EMotionCardType type) => new(type.ToString());

        /// <inheritdoc/>
        public bool Equals(MotionDriverId other)
            => StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is MotionDriverId other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => Value == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        /// <inheritdoc/>
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(MotionDriverId left, MotionDriverId right) => left.Equals(right);

        public static bool operator !=(MotionDriverId left, MotionDriverId right) => !left.Equals(right);
    }
}
