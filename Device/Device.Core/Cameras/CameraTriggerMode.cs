using System.Text.Json.Serialization;

namespace Device.Core.Cameras;

/// <summary>
/// 一路相机触发器的启用状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CameraTriggerMode>))]
public enum CameraTriggerMode
{
    /// <summary>关闭指定触发器，由相机自由运行或由其他触发器控制。</summary>
    Off,

    /// <summary>开启指定触发器，并使用配置的触发源。</summary>
    On,
}
