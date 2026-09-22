namespace MotionControl
{
    /// <summary>
    /// 运动设备最小生命周期接口。
    /// </summary>
    public interface IMotionDevice
    {
        /// <summary>设备名称。</summary>
        string Name { get; set; }

        /// <summary>连接设备。</summary>
        bool Connect();

        /// <summary>断开设备。</summary>
        bool DisConnect();
    }
}
