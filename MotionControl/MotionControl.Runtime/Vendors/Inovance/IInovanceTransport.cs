namespace MotionControl.Runtime.Vendors.Inovance
{
    /// <summary>
    /// 汇川驱动与控制器传输实现之间的内部 seam。
    /// 生产 adapter 隐藏寄存器地址、数值编码和读改写协议，测试使用内存 adapter。
    /// </summary>
    internal interface IInovanceTransport : IDisposable
    {
        bool IsConnected { get; }
        bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes);
        void Disconnect();
        bool TryReadStatus(int axis, out ushort status);
        bool UpdateControlWord(int axis, ushort setBits, ushort clearMask);
        bool WriteTarget(int axis, int position, float velocity);
        bool TryReadCurrentPosition(int axis, out int position);
        bool SetCurrentPosition(int axis, int position);
        bool TryReadInput(int index, out bool state);
        bool TryReadOutput(int index, out bool state);
        bool WriteOutput(int index, bool state);
    }
}

