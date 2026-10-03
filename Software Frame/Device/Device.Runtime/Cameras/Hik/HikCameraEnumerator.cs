// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：HikCameraEnumerator。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Cameras、Device.Core.Cameras、Device.Core.Devices、MvCamCtrl.NET、MvCamCtrl.NET.CameraParams。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Cameras;
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FormatIp。
    /// 正文中的关键协作调用包括 Join；具体条件和先后次序见方法体。
    /// </summary>
    private static string FormatIp(uint address) => string.Join(
        '.',
        (address & 0xff000000) >> 24,
        (address & 0x00ff0000) >> 16,
        (address & 0x0000ff00) >> 8,
        address & 0x000000ff);
}
