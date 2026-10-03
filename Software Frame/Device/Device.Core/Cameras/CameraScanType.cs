// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraScanType。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using System.Text.Json.Serialization;

namespace Device.Core.Cameras;

/// <summary>
/// 相机的成像扫描类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CameraScanType>))]
public enum CameraScanType
{
    /// <summary>面阵相机，一帧对应一次二维曝光。</summary>
    AreaScan,

    /// <summary>线扫相机，通过连续行采集组成完整图像。</summary>
    LineScan,
}
