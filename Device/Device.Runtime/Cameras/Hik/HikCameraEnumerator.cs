using Device.Core.Cameras;
using Device.Core.Devices;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;

namespace Device.Runtime.Cameras.Hik;

/// <summary>
/// 基于海康 MVS SDK 的 GigE 相机枚举器。
/// </summary>
internal static class HikCameraEnumerator
{
    /// <summary>枚举在线海康 GigE 相机，并使用工厂注册的稳定驱动标识描述设备。</summary>
    /// <param name="driverId">由统一相机工厂传入的驱动标识。</param>
    public static IReadOnlyList<CameraDeviceDescriptor> Enumerate(DeviceDriverId driverId)
    {
        var devices = new List<CCameraInfo>();
        var resultCode = CSystem.EnumDevices(CSystem.MV_GIGE_DEVICE, ref devices);
        if (resultCode != CErrorDefine.MV_OK)
            throw new InvalidOperationException($"枚举 GigE 相机失败，SDK 错误码：0x{resultCode:X8}。");

        var result = new List<CameraDeviceDescriptor>();
        foreach (var device in devices)
        {
            if (device is not CGigECameraInfo gigeInfo)
                continue;

            var ip = FormatIp(gigeInfo.nCurrentIp);
            var model = gigeInfo.chModelName?.Trim() ?? string.Empty;
            var serial = gigeInfo.chSerialNumber?.Trim() ?? string.Empty;
            result.Add(new CameraDeviceDescriptor(
                driverId,
                "Hikvision",
                model,
                serial,
                ip,
                string.IsNullOrEmpty(model) ? $"海康相机 · {ip}" : $"{model} · {ip}"));
        }

        return result;
    }

    private static string FormatIp(uint address) => string.Join(
        '.',
        (address & 0xff000000) >> 24,
        (address & 0x00ff0000) >> 16,
        (address & 0x0000ff00) >> 8,
        address & 0x000000ff);
}
