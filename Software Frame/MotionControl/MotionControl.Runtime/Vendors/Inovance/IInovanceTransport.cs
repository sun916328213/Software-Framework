// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：IInovanceTransport。成员说明可结合下方具体实现阅读。
namespace MotionControl.Runtime.Vendors.Inovance
{
    /// <summary>
    /// 汇川驱动与控制器传输实现之间的内部 seam。
    /// 生产 adapter 隐藏寄存器地址、数值编码和读改写协议，测试使用内存 adapter。
    /// </summary>
    internal interface IInovanceTransport : IDisposable
    {
        bool IsConnected { get; }
        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// </summary>
        bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes);
        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// </summary>
        void Disconnect();
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadStatus。
        /// </summary>
        bool TryReadStatus(int axis, out ushort status);
        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateControlWord。
        /// </summary>
        bool UpdateControlWord(int axis, ushort setBits, ushort clearMask);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteTarget。
        /// </summary>
        bool WriteTarget(int axis, int position, float velocity);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadCurrentPosition。
        /// </summary>
        bool TryReadCurrentPosition(int axis, out int position);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentPosition。
        /// </summary>
        bool SetCurrentPosition(int axis, int position);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadInput。
        /// </summary>
        bool TryReadInput(int index, out bool state);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadOutput。
        /// </summary>
        bool TryReadOutput(int index, out bool state);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// </summary>
        bool WriteOutput(int index, bool state);
    }
}

