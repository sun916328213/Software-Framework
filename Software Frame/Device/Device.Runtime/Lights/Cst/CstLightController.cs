// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CstLightController、NativeMethods。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Lights、System.Runtime.InteropServices、Device.Core.Lights。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Lights;
using System.Runtime.InteropServices;
using Device.Core.Lights;

namespace Device.Runtime.Lights.Cst;

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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectCore。
    /// 正文中的关键协作调用包括 Parse、AsSpan、Trim、CreateSerialPort；具体条件和先后次序见方法体。
    /// </remarks>
    protected override int ConnectCore()
    {
        var portNumber = int.Parse(Options.PortName.Trim().AsSpan(3));
        return NativeMethods.CreateSerialPort(portNumber, out _controllerHandle);
    }

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
    /// 正文中的关键协作调用包括 SetDigitalValue；具体条件和先后次序见方法体。
    /// </remarks>
    protected override int SetIntensityCore(int channel, int intensity) =>
        NativeMethods.SetDigitalValue(channel, intensity, _controllerHandle);

    /// <summary>
    /// NativeMethods 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
    /// </summary>
    private static class NativeMethods
    {
        private const string LibraryName = "CSTControllerDll.dll";

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateSerialPort。
        /// </summary>
        [DllImport(LibraryName, EntryPoint = "CST_CreateSerialPort", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int CreateSerialPort(int serialPortIndex, out long controllerHandle);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReleaseSerialPort。
        /// </summary>
        [DllImport(LibraryName, EntryPoint = "CST_ReleaseSerialPort", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReleaseSerialPort(long controllerHandle);

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetDigitalValue。
        /// </summary>
        [DllImport(LibraryName, EntryPoint = "CST_SetDigitalValue", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetDigitalValue(int channel, int intensity, long controllerHandle);
    }
}
