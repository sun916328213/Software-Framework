using System.Text.Json.Serialization;
using Device.Core.Serialization;

namespace Device.Core.Devices;

/// <summary>
/// 设备驱动的稳定标识。
/// </summary>
/// <remarks>
/// 配置文件应保存该标识，而不是保存品牌枚举或界面显示名称。
/// 例如：<c>camera.hik.gige</c>。
/// </remarks>
[JsonConverter(typeof(DeviceDriverIdJsonConverter))]
public readonly record struct DeviceDriverId
{
    /// <summary>
    /// 创建驱动标识，并执行去除首尾空白和小写规范化。
    /// </summary>
    /// <param name="value">不可为空的驱动标识。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空或仅包含空白字符。</exception>
    public DeviceDriverId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Driver id cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// 经过清理并统一为小写的驱动标识值。
    /// </summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value;
}
