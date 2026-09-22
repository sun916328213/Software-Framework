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
