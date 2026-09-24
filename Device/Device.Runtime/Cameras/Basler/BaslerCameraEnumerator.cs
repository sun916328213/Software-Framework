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
