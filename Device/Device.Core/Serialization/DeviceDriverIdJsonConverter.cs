using System.Text.Json;
using System.Text.Json.Serialization;
using Device.Core.Devices;

namespace Device.Core.Serialization;

/// <summary>
/// 将 <see cref="DeviceDriverId"/> 序列化为简洁、稳定的 JSON 字符串。
/// </summary>
/// <remarks>
/// 配置文件中的格式为 <c>"camera.hik.gige"</c>，而不是包含 Value 属性的对象。
/// </remarks>
public sealed class DeviceDriverIdJsonConverter : JsonConverter<DeviceDriverId>
{
    /// <inheritdoc />
    public override DeviceDriverId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("驱动标识必须是 JSON 字符串。");

        var value = reader.GetString();
        try
        {
            return new DeviceDriverId(value ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("驱动标识不能为空。", exception);
        }
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        DeviceDriverId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
