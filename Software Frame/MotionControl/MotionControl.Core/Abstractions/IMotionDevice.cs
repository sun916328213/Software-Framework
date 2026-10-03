// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：IMotionDevice。成员说明可结合下方具体实现阅读。
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
