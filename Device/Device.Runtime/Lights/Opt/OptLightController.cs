using System.Runtime.InteropServices;
using Device.Core.Lights;

namespace Device.Runtime.Lights.Opt;

/// <summary>
/// 直接调用 OPT 原生函数的串口光源控制器适配器。
/// </summary>
internal sealed class OptLightController : SerialLightControllerBase
{
    private const int OptSuccess = 0;
    private long _controllerHandle;

    /// <summary>使用 OPT 光源配置创建适配器。</summary>
    /// <param name="options">已经验证的光源配置。</param>
    public OptLightController(LightControllerOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override string ErrorCodePrefix => "light.opt";

    /// <inheritdoc />
    protected override int SuccessCode => OptSuccess;

    /// <inheritdoc />
    protected override int ConnectCore() =>
        NativeMethods.InitializeSerialPort(Options.PortName.Trim().ToUpperInvariant(), out _controllerHandle);

    /// <inheritdoc />
    protected override int DisconnectCore()
    {
        var result = NativeMethods.ReleaseSerialPort(_controllerHandle);
        _controllerHandle = 0;
        return result;
    }

    /// <inheritdoc />
    protected override int SetIntensityCore(int channel, int intensity) =>
        NativeMethods.SetIntensity(_controllerHandle, channel, intensity);

    private static class NativeMethods
    {
        private const string LibraryName = "OPTController.dll";

        [DllImport(
            LibraryName,
            EntryPoint = "OPTController_InitSerialPort",
            CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi)]
        internal static extern int InitializeSerialPort(string portName, out long controllerHandle);

        [DllImport(LibraryName, EntryPoint = "OPTController_ReleaseSerialPort", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReleaseSerialPort(long controllerHandle);

        [DllImport(LibraryName, EntryPoint = "OPTController_SetIntensity", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetIntensity(long controllerHandle, int channel, int intensity);
    }
}
