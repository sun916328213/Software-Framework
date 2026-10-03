// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ITekCameraEnumerator。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Cameras、Device.Core.Cameras、Device.Core.Devices、IKapC.NET。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Cameras;
using Device.Core.Cameras;
using Device.Core.Devices;
using IKapC.NET;

namespace Device.Runtime.Cameras.ITek;

/// <summary>
/// 基于 I-Tek IKapC SDK 的 GigE 相机枚举器。
/// </summary>
internal static class ITekCameraEnumerator
{
    /// <summary>枚举在线 I-Tek GigE 相机，并使用工厂注册的稳定驱动标识描述设备。</summary>
    /// <param name="driverId">由统一相机工厂传入的驱动标识。</param>
    public static IReadOnlyList<CameraDeviceDescriptor> Enumerate(DeviceDriverId driverId)
    {
        ITekGigeCamera.AcquireEnvironment();
        try
        {
            uint deviceCount = 0;
            var resultCode = IKapCLib.ItkManGetDeviceCount(ref deviceCount);
            if (resultCode != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                throw new InvalidOperationException($"枚举 I-Tek 相机失败，SDK 错误码：0x{resultCode:X8}。");

            var result = new List<CameraDeviceDescriptor>();
            for (uint index = 0; index < deviceCount; index++)
            {
                var deviceInfo = new IKapCLib.ITKDEV_INFO();
                resultCode = IKapCLib.ItkManGetDeviceInfo(index, ref deviceInfo);
                if (resultCode != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                    continue;

                if (!string.Equals(deviceInfo.DeviceClass, "GigEVision", StringComparison.OrdinalIgnoreCase))
                    continue;

                var gigeInfo = new IKapCLib.ITKGIGEDEV_INFO();
                resultCode = IKapCLib.ItkManGetGigEDeviceInfo(index, ref gigeInfo);
                if (resultCode != (uint)ItkStatusErrorId.ITKSTATUS_OK)
                    continue;

                var ip = gigeInfo.Ip?.Trim() ?? string.Empty;
                var model = deviceInfo.ModelName?.Trim() ?? string.Empty;
                var serial = deviceInfo.SerialNumber?.Trim() ?? string.Empty;
                result.Add(new CameraDeviceDescriptor(
                    driverId,
                    "I-Tek",
                    model,
                    serial,
                    ip,
                    string.IsNullOrEmpty(model) ? $"I-Tek 相机 · {ip}" : $"{model} · {ip}"));
            }

            return result;
        }
        finally
        {
            ITekGigeCamera.ReleaseEnvironment();
        }
    }
}
