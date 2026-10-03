// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：MultiCamNative、SignalInfo、NativeMethods。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.FrameGrabbers、System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
using Device.Runtime.FrameGrabbers;
using System.Runtime.InteropServices;

namespace Device.Runtime.FrameGrabbers.Euresys;

/// <summary>
/// 新架构实际使用到的 Euresys MultiCam 原生接口最小封装。
/// </summary>
internal static class MultiCamNative
{
    internal const uint Configuration = 0x20000000;
    internal const uint SignalEnable = 24u << 14;
    internal const int SurfaceProcessingSignal = 1;
    internal const int AcquisitionFailureSignal = 7;
    private const int MaximumErrorTextLength = 1_024;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void Callback(ref SignalInfo signalInfo);

    /// <summary>
    /// SignalInfo 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SignalInfo
    {
        internal IntPtr Context;
        internal uint Instance;
        internal int Signal;
        internal uint SignalInfoValue;
        internal uint SignalContext;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 OpenDriver。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McOpenDriver；具体条件和先后次序见方法体。
    /// </summary>
    internal static void OpenDriver() =>
        ThrowIfFailed(NativeMethods.McOpenDriver(IntPtr.Zero), "无法打开 MultiCam 驱动");

    /// <summary>
    /// 执行关闭步骤；对应入口为 CloseDriver。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McCloseDriver；具体条件和先后次序见方法体。
    /// </summary>
    internal static void CloseDriver() =>
        ThrowIfFailed(NativeMethods.McCloseDriver(), "无法关闭 MultiCam 驱动");

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateChannel。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McCreateNm；具体条件和先后次序见方法体。
    /// </summary>
    internal static uint CreateChannel()
    {
        ThrowIfFailed(NativeMethods.McCreateNm("CHANNEL", out var channel), "无法创建 MultiCam 通道");
        return channel;
    }

    /// <summary>
    /// 执行删除操作；对应入口为 Delete。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McDelete；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Delete(uint instance) =>
        ThrowIfFailed(NativeMethods.McDelete(instance), $"无法删除 MultiCam 实例 {instance}");

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 Set。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McSetParamNmInt；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Set(uint instance, string parameterName, int value) =>
        ThrowIfFailed(
            NativeMethods.McSetParamNmInt(instance, parameterName, value),
            $"无法设置 MultiCam 参数 {parameterName}");

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 Set。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McSetParamStr；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Set(uint instance, uint parameterId, string value) =>
        ThrowIfFailed(
            NativeMethods.McSetParamStr(instance, parameterId, value),
            $"无法设置 MultiCam 参数 {parameterId}");

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 Set。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McSetParamNmStr；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Set(uint instance, string parameterName, string value) =>
        ThrowIfFailed(
            NativeMethods.McSetParamNmStr(instance, parameterName, value),
            $"无法设置 MultiCam 参数 {parameterName}");

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetInt32。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McGetParamNmInt；具体条件和先后次序见方法体。
    /// </summary>
    internal static int GetInt32(uint instance, string parameterName)
    {
        ThrowIfFailed(
            NativeMethods.McGetParamNmInt(instance, parameterName, out var value),
            $"无法读取 MultiCam 参数 {parameterName}");
        return value;
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetPointer。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McGetParamNmPtr；具体条件和先后次序见方法体。
    /// </summary>
    internal static IntPtr GetPointer(uint instance, string parameterName)
    {
        ThrowIfFailed(
            NativeMethods.McGetParamNmPtr(instance, parameterName, out var value),
            $"无法读取 MultiCam 参数 {parameterName}");
        return value;
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterCallback。
    /// 正文中的关键协作调用包括 ThrowIfFailed、McRegisterCallback；具体条件和先后次序见方法体。
    /// </summary>
    internal static void RegisterCallback(uint instance, Callback callback, uint context) =>
        ThrowIfFailed(
            NativeMethods.McRegisterCallback(instance, callback, context),
            "无法注册 MultiCam 回调");

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ThrowIfFailed。
    /// 正文中的关键协作调用包括 GetErrorMessage；具体条件和先后次序见方法体。
    /// </summary>
    private static void ThrowIfFailed(int status, string action)
    {
        if (status == 0)
            return;

        throw new InvalidOperationException($"{action}：{GetErrorMessage(status)}");
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetErrorMessage。
    /// 正文中的关键协作调用包括 AllocHGlobal、Abs、McGetParamStr、PtrToStringAnsi、FreeHGlobal；具体条件和先后次序见方法体。
    /// </summary>
    private static string GetErrorMessage(int errorCode)
    {
        const uint errorDescription = 98u << 14;
        var text = Marshal.AllocHGlobal(MaximumErrorTextLength + 1);
        try
        {
            var status = (uint)Math.Abs(errorCode);
            if (NativeMethods.McGetParamStr(
                    Configuration,
                    errorDescription + status,
                    text,
                    MaximumErrorTextLength) != 0)
                return $"未知错误 {errorCode}";

            return Marshal.PtrToStringAnsi(text) ?? $"未知错误 {errorCode}";
        }
        finally
        {
            Marshal.FreeHGlobal(text);
        }
    }

    /// <summary>
    /// NativeMethods 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McOpenDriver。
        /// </summary>
        [DllImport("MultiCam.dll")]
        internal static extern int McOpenDriver(IntPtr instanceName);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McCloseDriver。
        /// </summary>
        [DllImport("MultiCam.dll")]
        internal static extern int McCloseDriver();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McCreateNm。
        /// </summary>
        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McCreateNm(string modelName, out uint instance);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McDelete。
        /// </summary>
        [DllImport("MultiCam.dll")]
        internal static extern int McDelete(uint instance);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McSetParamNmInt。
        /// </summary>
        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McSetParamNmInt(uint instance, string parameterName, int value);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McSetParamStr。
        /// </summary>
        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McSetParamStr(uint instance, uint parameterId, string value);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McSetParamNmStr。
        /// </summary>
        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McSetParamNmStr(uint instance, string parameterName, string value);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McGetParamNmInt。
        /// </summary>
        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McGetParamNmInt(uint instance, string parameterName, out int value);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McGetParamNmPtr。
        /// </summary>
        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McGetParamNmPtr(uint instance, string parameterName, out IntPtr value);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McGetParamStr。
        /// </summary>
        [DllImport("MultiCam.dll")]
        internal static extern int McGetParamStr(
            uint instance,
            uint parameterId,
            IntPtr value,
            uint maximumLength);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 McRegisterCallback。
        /// </summary>
        [DllImport("MultiCam.dll")]
        internal static extern int McRegisterCallback(
            uint instance,
            Callback callbackFunction,
            uint context);
    }
}
