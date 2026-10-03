// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：OptLightController、NativeMethods。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Lights、System.Runtime.InteropServices、Device.Core.Lights。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Lights;
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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectCore。
    /// 正文中的关键协作调用包括 InitializeSerialPort、ToUpperInvariant、Trim；具体条件和先后次序见方法体。
    /// </remarks>
    protected override int ConnectCore() =>
        NativeMethods.InitializeSerialPort(Options.PortName.Trim().ToUpperInvariant(), out _controllerHandle);

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectCore。
    /// 正文中的关键协作调用包括 ReleaseSerialPort；具体条件和先后次序见方法体。
    /// </remarks>
    protected override int DisconnectCore()
    {
        var result = NativeMethods.ReleaseSerialPort(_controllerHandle);
        _controllerHandle = 0;
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 设置当前实现管理的值或状态；对应入口为 SetIntensityCore。
    /// 正文中的关键协作调用包括 SetIntensity；具体条件和先后次序见方法体。
    /// </remarks>
    protected override int SetIntensityCore(int channel, int intensity) =>
        NativeMethods.SetIntensity(_controllerHandle, channel, intensity);

    /// <summary>
    /// NativeMethods 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
    /// </summary>
    private static class NativeMethods
    {
        private const string LibraryName = "OPTController.dll";

        /// <summary>
        /// 执行初始化步骤；对应入口为 InitializeSerialPort。
        /// </summary>
        [DllImport(
            LibraryName,
            EntryPoint = "OPTController_InitSerialPort",
            CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi)]
        internal static extern int InitializeSerialPort(string portName, out long controllerHandle);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReleaseSerialPort。
        /// </summary>
        [DllImport(LibraryName, EntryPoint = "OPTController_ReleaseSerialPort", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReleaseSerialPort(long controllerHandle);

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetIntensity。
        /// </summary>
        [DllImport(LibraryName, EntryPoint = "OPTController_SetIntensity", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetIntensity(long controllerHandle, int channel, int intensity);
    }
}
