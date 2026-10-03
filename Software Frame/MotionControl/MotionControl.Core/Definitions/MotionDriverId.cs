// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionDriverId。成员说明可结合下方具体实现阅读。
namespace MotionControl.Core.Definitions;

/// <summary>
/// 稳定的运动控制驱动标识。
/// <para>比较时忽略大小写；第三方驱动可使用反向域名形式，例如 company.motion.driver。</para>
/// <para>P1 从旧 <c>MotionControl.Definition.MotionDriverId</c> 迁移，语义不变。</para>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Equals。
    /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
    /// </remarks>
    public bool Equals(MotionDriverId other)
        => StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Equals。
    /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
    /// </remarks>
    public override bool Equals(object? obj) => obj is MotionDriverId other && Equals(other);

    /// <inheritdoc/>
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 GetHashCode。
    /// 正文中的关键协作调用包括 GetHashCode；具体条件和先后次序见方法体。
    /// </remarks>
    public override int GetHashCode()
        => Value == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ToString。
    /// </remarks>
    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(MotionDriverId left, MotionDriverId right) => left.Equals(right);

    public static bool operator !=(MotionDriverId left, MotionDriverId right) => !left.Equals(right);
}
