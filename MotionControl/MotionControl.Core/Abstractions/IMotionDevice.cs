namespace MotionControl.Core.Abstractions;

/// <summary>
/// 运动设备最小生命周期接口。
/// <para>P1 从旧 <c>MotionControl.IMotionDevice</c> 迁移，语义不变，仅命名空间变更。</para>
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
