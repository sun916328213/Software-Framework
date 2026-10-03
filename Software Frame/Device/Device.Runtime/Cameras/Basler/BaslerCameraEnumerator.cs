// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：BaslerCameraEnumerator。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Cameras、Basler.Pylon、Device.Core.Cameras、Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Cameras;
using Basler.Pylon;
using Device.Core.Cameras;
using Device.Core.Devices;

namespace Device.Runtime.Cameras.Basler;

/// <summary>
/// 基于 Basler Pylon SDK 的相机枚举器。
/// </summary>
internal static class BaslerCameraEnumerator
{
    /// <summary>枚举在线 Basler 相机，并使用工厂注册的稳定驱动标识描述设备。</summary>
    /// <param name="driverId">由统一相机工厂传入的驱动标识。</param>
    public static IReadOnlyList<CameraDeviceDescriptor> Enumerate(DeviceDriverId driverId)
    {
        var result = new List<CameraDeviceDescriptor>();
        foreach (var info in CameraFinder.Enumerate())
        {
            var ip = TryGet(info, CameraInfoKey.DeviceIpAddress);
            var model = TryGet(info, CameraInfoKey.ModelName);
            var serial = TryGet(info, CameraInfoKey.SerialNumber);
            result.Add(new CameraDeviceDescriptor(
                driverId,
                "Basler",
                model,
                serial,
                ip,
                string.IsNullOrEmpty(model) ? $"Basler 相机 · {ip}" : $"{model} · {ip}"));
        }

        return result;
    }

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGet。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static string TryGet(ICameraInfo info, string key)
    {
        try
        {
            return info[key]?.Trim() ?? string.Empty;
        }
        catch (Exception)
        {
            // 非 GigE 设备（如 USB）没有 IP 键，缺什么留空即可。
            return string.Empty;
        }
    }
}
