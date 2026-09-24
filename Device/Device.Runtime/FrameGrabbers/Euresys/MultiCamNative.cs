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

    [StructLayout(LayoutKind.Sequential)]
    internal struct SignalInfo
    {
        internal IntPtr Context;
        internal uint Instance;
        internal int Signal;
        internal uint SignalInfoValue;
        internal uint SignalContext;
    }

    internal static void OpenDriver() =>
        ThrowIfFailed(NativeMethods.McOpenDriver(IntPtr.Zero), "无法打开 MultiCam 驱动");

    internal static void CloseDriver() =>
        ThrowIfFailed(NativeMethods.McCloseDriver(), "无法关闭 MultiCam 驱动");

    internal static uint CreateChannel()
    {
        ThrowIfFailed(NativeMethods.McCreateNm("CHANNEL", out var channel), "无法创建 MultiCam 通道");
        return channel;
    }

    internal static void Delete(uint instance) =>
        ThrowIfFailed(NativeMethods.McDelete(instance), $"无法删除 MultiCam 实例 {instance}");

    internal static void Set(uint instance, string parameterName, int value) =>
        ThrowIfFailed(
            NativeMethods.McSetParamNmInt(instance, parameterName, value),
            $"无法设置 MultiCam 参数 {parameterName}");

    internal static void Set(uint instance, uint parameterId, string value) =>
        ThrowIfFailed(
            NativeMethods.McSetParamStr(instance, parameterId, value),
            $"无法设置 MultiCam 参数 {parameterId}");

    internal static void Set(uint instance, string parameterName, string value) =>
        ThrowIfFailed(
            NativeMethods.McSetParamNmStr(instance, parameterName, value),
            $"无法设置 MultiCam 参数 {parameterName}");

    internal static int GetInt32(uint instance, string parameterName)
    {
        ThrowIfFailed(
            NativeMethods.McGetParamNmInt(instance, parameterName, out var value),
            $"无法读取 MultiCam 参数 {parameterName}");
        return value;
    }

    internal static IntPtr GetPointer(uint instance, string parameterName)
    {
        ThrowIfFailed(
            NativeMethods.McGetParamNmPtr(instance, parameterName, out var value),
            $"无法读取 MultiCam 参数 {parameterName}");
        return value;
    }

    internal static void RegisterCallback(uint instance, Callback callback, uint context) =>
        ThrowIfFailed(
            NativeMethods.McRegisterCallback(instance, callback, context),
            "无法注册 MultiCam 回调");

    private static void ThrowIfFailed(int status, string action)
    {
        if (status == 0)
            return;

        throw new InvalidOperationException($"{action}：{GetErrorMessage(status)}");
    }

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

    private static class NativeMethods
    {
        [DllImport("MultiCam.dll")]
        internal static extern int McOpenDriver(IntPtr instanceName);

        [DllImport("MultiCam.dll")]
        internal static extern int McCloseDriver();

        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McCreateNm(string modelName, out uint instance);

        [DllImport("MultiCam.dll")]
        internal static extern int McDelete(uint instance);

        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McSetParamNmInt(uint instance, string parameterName, int value);

        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McSetParamStr(uint instance, uint parameterId, string value);

        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McSetParamNmStr(uint instance, string parameterName, string value);

        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McGetParamNmInt(uint instance, string parameterName, out int value);

        [DllImport("MultiCam.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern int McGetParamNmPtr(uint instance, string parameterName, out IntPtr value);

        [DllImport("MultiCam.dll")]
        internal static extern int McGetParamStr(
            uint instance,
            uint parameterId,
            IntPtr value,
            uint maximumLength);

        [DllImport("MultiCam.dll")]
        internal static extern int McRegisterCallback(
            uint instance,
            Callback callbackFunction,
            uint context);
    }
}
