// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：EMotionResult、EDirection、EStopMode、EHomeMode。成员说明可结合下方具体实现阅读。
namespace MotionControl.Core.Definitions;

/// <summary>
/// 运动控制卡执行结果（0 表示成功）
/// <para>P1 从旧 <c>MotionControl.Definition.EMotionResult</c> 迁移，语义不变。</para>
/// </summary>
public enum EMotionResult
{
    /// <summary>
    /// 成功
    /// </summary>
    Success = 0,
    /// <summary>
    /// 失败
    /// </summary>
    Fail = 1,
    /// <summary>
    /// 卡未连接
    /// </summary>
    NotConnected = 2,
    /// <summary>
    /// 轴索引非法
    /// </summary>
    InvalidAxis = 3,
    /// <summary>
    /// 轴正忙（运动中不允许的操作）
    /// </summary>
    Busy = 4,
    /// <summary>
    /// 参数错误
    /// </summary>
    InvalidParam = 5,
    /// <summary>
    /// 操作超时
    /// </summary>
    Timeout = 6,
    /// <summary>
    /// 操作被取消
    /// </summary>
    Cancelled = 7,
    /// <summary>
    /// 轴报警
    /// </summary>
    Alarm = 8,
    /// <summary>
    /// 急停触发
    /// </summary>
    EmergencyStop = 9,
    /// <summary>
    /// 当前实现不支持该能力
    /// </summary>
    NotSupported = 10,
}

/// <summary>
/// 方向（点动/回零方向）
/// </summary>
public enum EDirection
{
    /// <summary>
    /// 负方向
    /// </summary>
    Negative = -1,
    /// <summary>
    /// 正方向
    /// </summary>
    Positive = 1,
}

/// <summary>
/// 停止方式
/// </summary>
public enum EStopMode
{
    /// <summary>
    /// 减速停止
    /// </summary>
    Decelerate = 0,
    /// <summary>
    /// 立即停止（急停）
    /// </summary>
    Emergency = 1,
}

/// <summary>
/// 回零模式
/// </summary>
public enum EHomeMode
{
    /// <summary>
    /// 不回零
    /// </summary>
    None = 0,
    /// <summary>
    /// 原点开关回零
    /// </summary>
    OriginSwitch = 1,
    /// <summary>
    /// 编码器Z相回零
    /// </summary>
    ZPhase = 2,
    /// <summary>
    /// 限位开关回零
    /// </summary>
    LimitSwitch = 3,
    /// <summary>
    /// 当前位置直接置零
    /// </summary>
    SetCurrentAsOrigin = 4,
}
