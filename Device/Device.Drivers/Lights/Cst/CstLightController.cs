using System.Runtime.InteropServices;
using Device.Core.Lights;

namespace Device.Drivers.Lights.Cst;

/// <summary>
/// 直接调用 CST 原生函数的串口光源控制器适配器。
/// </summary>
internal sealed class CstLightController : SerialLightControllerBase
{
    private const int CstSuccess = 10_000;
    private long _controllerHandle;

    /// <summary>使用 CST 光源配置创建适配器。</summary>
    /// <param name="options">已经验证的光源配置。</param>
    public CstLightController(LightControllerOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override string ErrorCodePrefix => "light.cst";

    /// <inheritdoc />
    protected override int SuccessCode => CstSuccess;

    /// <inheritdoc />
    protected override int ConnectCore()
    {
        var portNumber = int.Parse(Options.PortName.Trim().AsSpan(3));
        return NativeMethods.CreateSerialPort(portNumber, out _controllerHandle);
    }

    /// <inheritdoc />
    protected override int DisconnectCore()
    {
        var result = NativeMethods.ReleaseSerialPort(_controllerHandle);
        _controllerHandle = 0;
        return result;
    }

    /// <inheritdoc />
    protected override int SetIntensityCore(int channel, int intensity) =>
        NativeMethods.SetDigitalValue(channel, intensity, _controllerHandle);

    private static class NativeMethods
    {
        private const string LibraryName = "CSTControllerDll.dll";

        [DllImport(LibraryName, EntryPoint = "CST_CreateSerialPort", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int CreateSerialPort(int serialPortIndex, out long controllerHandle);

        [DllImport(LibraryName, EntryPoint = "CST_ReleaseSerialPort", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReleaseSerialPort(long controllerHandle);

        [DllImport(LibraryName, EntryPoint = "CST_SetDigitalValue", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetDigitalValue(int channel, int intensity, long controllerHandle);
    }
}
