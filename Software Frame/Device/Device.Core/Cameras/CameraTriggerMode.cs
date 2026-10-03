// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraTriggerMode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
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
