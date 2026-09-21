using System.Runtime.InteropServices;
using Device.Core.Cameras;
using Device.Core.Devices;
using MVSDK_Net;

namespace Device.Drivers.Cameras.Dahua;

/// <summary>
/// 基于大华 MV SDK 的 GigE 相机枚举器。
/// </summary>
internal static class DahuaCameraEnumerator
{
    /// <summary>枚举在线大华 GigE 相机，并使用工厂注册的稳定驱动标识描述设备。</summary>
    /// <param name="driverId">由统一相机工厂传入的驱动标识。</param>
    public static IReadOnlyList<CameraDeviceDescriptor> Enumerate(DeviceDriverId driverId)
    {
        var deviceList = new IMVDefine.IMV_DeviceList();
        var resultCode = MyCamera.IMV_EnumDevices(
            ref deviceList,
            (uint)IMVDefine.IMV_EInterfaceType.interfaceTypeGige);
        if (resultCode != IMVDefine.IMV_OK)
            throw new InvalidOperationException($"枚举 GigE 相机失败，SDK 错误码：0x{resultCode:X8}。");

        var result = new List<CameraDeviceDescriptor>();
        if (deviceList.pDevInfo == IntPtr.Zero)
            return result;

        var deviceSize = Marshal.SizeOf<IMVDefine.IMV_DeviceInfo>();
        for (var index = 0u; index < deviceList.nDevNum; index++)
        {
            var info = Marshal.PtrToStructure<IMVDefine.IMV_DeviceInfo>(
                IntPtr.Add(deviceList.pDevInfo, checked((int)(index * deviceSize))));

            var ip = ReadGigEAddress(in info);
            var model = info.modelName?.Trim() ?? string.Empty;
            var serial = info.serialNumber?.Trim() ?? string.Empty;
            result.Add(new CameraDeviceDescriptor(
                driverId,
                "Dahua",
                model,
                serial,
                ip,
                string.IsNullOrEmpty(model) ? $"大华相机 · {ip}" : $"{model} · {ip}"));
        }

        return result;
    }

    /// <summary>从设备信息内嵌的 GigE 原始结构缓冲区中读取 IP。</summary>
    private static string ReadGigEAddress(in IMVDefine.IMV_DeviceInfo info)
    {
        var buffer = info.deviceSpecificInfo.gigeDeviceInfo;
        if (buffer is null || buffer.Length < Marshal.SizeOf<IMVDefine.IMV_GigEDeviceInfo>())
            return string.Empty;

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var gige = Marshal.PtrToStructure<IMVDefine.IMV_GigEDeviceInfo>(handle.AddrOfPinnedObject());
            return gige.ipAddress?.Trim() ?? string.Empty;
        }
        finally
        {
            handle.Free();
        }
    }
}
