// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DeviceDriverIdJsonConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、System.Text.Json.Serialization、Device.Core.Devices。导入不代表所有类型都会在运行时加载。
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
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// 正文中的关键协作调用包括 GetString；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 执行写入操作；对应入口为 Write。
    /// 正文中的关键协作调用包括 WriteStringValue；具体条件和先后次序见方法体。
    /// </remarks>
    public override void Write(
        Utf8JsonWriter writer,
        DeviceDriverId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
