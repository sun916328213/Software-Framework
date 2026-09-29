using MotionControl.Core.Definitions;

namespace MotionControl.Core.Abstractions;

/// <summary>
/// 运动控制卡接口。
/// <para>P1 从旧 <c>MotionControl.IMotionCard</c> 迁移，语义不变，仅命名空间变更。</para>
/// <para>迁移前已检查：接口仅暴露 <c>MotionControl.Core.Definitions</c> 通用类型，未直接暴露具体厂商类型/SDK/WPF 类型。</para>
/// </summary>
public interface IMotionCard : IMotionDevice
{
    /// <summary>
    /// 运动控制卡待连接参数。连接时创建运行快照，连接期间的外部修改在下次连接生效。
    /// </summary>
    MotionCardParam Param { get; set; }
    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }
    /// <summary>
    /// 轴数量
    /// </summary>
    int AxisCount { get; }
    /// <summary>
    /// 轴报警触发事件（由具体实现触发）
    /// </summary>
    event EventHandler<AxisAlarmEventArgs>? AlarmOccurred;

    /// <summary>连接控制卡，并返回可诊断的参数或驱动错误。</summary>
    MotionResult ConnectResult()
        => Connect()
            ? MotionResult.Success()
            : MotionResult.Failure(EMotionResult.Fail, "控制卡连接失败。");

    /// <summary>断开控制卡，并返回可诊断的驱动错误。</summary>
    MotionResult DisconnectResult()
        => DisConnect()
            ? MotionResult.Success()
            : MotionResult.Failure(EMotionResult.Fail, "控制卡断开失败。");

    #region 伺服控制
    /// <summary>
    /// 伺服使能
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool ServoOn(int axis);
    /// <summary>伺服使能，并保留失败原因。</summary>
    MotionResult ServoOnResult(int axis)
        => ServoOn(axis) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "伺服使能失败。");
    /// <summary>
    /// 伺服失能
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool ServoOff(int axis);
    /// <summary>伺服失能，并保留失败原因。</summary>
    MotionResult ServoOffResult(int axis)
        => ServoOff(axis) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "伺服失能失败。");
    /// <summary>
    /// 清除轴状态（报警、限位等）
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool ClearAxisStatus(int axis);
    /// <summary>清除轴状态，并保留失败原因。</summary>
    MotionResult ClearAxisStatusResult(int axis)
        => ClearAxisStatus(axis) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "清除轴状态失败。");
    #endregion

    #region 运动控制
    /// <summary>
    /// 设置轴运行速度
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="velocity">速度（用户单位/s）</param>
    bool SetSpeed(int axis, double velocity);
    /// <summary>设置轴速度，并保留失败原因。</summary>
    MotionResult SetSpeedResult(int axis, double velocity)
        => SetSpeed(axis, velocity) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "设置轴速度失败。");
    /// <summary>
    /// 绝对运动到目标位置
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="position">目标位置（用户单位）</param>
    bool MoveAbs(int axis, double position);
    /// <summary>绝对运动，并保留失败原因。</summary>
    MotionResult MoveAbsResult(int axis, double position)
        => MoveAbs(axis, position) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "绝对运动失败。");
    /// <summary>
    /// 相对当前位置运动
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="distance">运动距离（用户单位，负值表示负方向）</param>
    bool MoveRel(int axis, double distance);
    /// <summary>相对运动，并保留失败原因。</summary>
    MotionResult MoveRelResult(int axis, double distance)
        => MoveRel(axis, distance) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "相对运动失败。");
    /// <summary>
    /// 开始点动（持续运动直到Stop）
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="direction">点动方向</param>
    bool JogStart(int axis, EDirection direction);
    /// <summary>开始点动，并保留失败原因。</summary>
    MotionResult JogStartResult(int axis, EDirection direction)
        => JogStart(axis, direction) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "开始点动失败。");
    /// <summary>
    /// 停止单轴
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="stopMode">停止方式，默认减速停</param>
    bool Stop(int axis, EStopMode stopMode = EStopMode.Decelerate);
    /// <summary>停止单轴，并保留失败原因。</summary>
    MotionResult StopResult(int axis, EStopMode stopMode = EStopMode.Decelerate)
        => Stop(axis, stopMode) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "停止单轴失败。");
    /// <summary>
    /// 停止本卡所有轴
    /// </summary>
    /// <param name="stopMode">停止方式，默认急停</param>
    bool StopAll(EStopMode stopMode = EStopMode.Emergency);
    /// <summary>停止全部轴，并保留失败原因。</summary>
    MotionResult StopAllResult(EStopMode stopMode = EStopMode.Emergency)
        => StopAll(stopMode) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "停止全部轴失败。");
    #endregion

    #region 回零
    /// <summary>
    /// 启动回零
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool StartHome(int axis);
    /// <summary>启动回零，并保留失败原因。</summary>
    MotionResult StartHomeResult(int axis)
        => StartHome(axis) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "启动回零失败。");
    /// <summary>
    /// 是否已完成回零
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool IsHomeDone(int axis);
    #endregion

    #region 状态查询
    /// <summary>
    /// 获取指令位置（规划位置）
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <returns>位置（用户单位），失败返回0</returns>
    double GetCommandPosition(int axis);
    /// <summary>
    /// 获取指令位置，并保留设备失败原因。
    /// </summary>
    MotionResult<double> ReadCommandPosition(int axis)
        => MotionResult<double>.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化读取接口。");
    /// <summary>
    /// 获取编码器位置（反馈位置）
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <returns>位置（用户单位），失败返回0</returns>
    double GetEncoderPosition(int axis);
    /// <summary>
    /// 获取编码器位置，并保留设备失败原因。
    /// </summary>
    MotionResult<double> ReadEncoderPosition(int axis)
        => MotionResult<double>.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化读取接口。");
    /// <summary>
    /// 获取轴完整状态
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    AxisStatus GetAxisStatus(int axis);
    /// <summary>
    /// 获取轴状态，并保留设备失败原因。
    /// </summary>
    MotionResult<AxisStatus> ReadAxisStatus(int axis)
        => MotionResult<AxisStatus>.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化读取接口。");
    /// <summary>
    /// 轴是否运动中
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool IsMoving(int axis);
    /// <summary>
    /// 轴是否到位
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    bool IsInPosition(int axis);
    #endregion

    #region IO控制
    /// <summary>
    /// 读取通用输入点
    /// </summary>
    /// <param name="index">输入点索引，从0开始</param>
    bool GetInput(int index);
    /// <summary>
    /// 读取输入点，并区分低电平与读取失败。
    /// </summary>
    MotionResult<bool> ReadInput(int index)
        => MotionResult<bool>.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化读取接口。");
    /// <summary>
    /// 读取通用输出点当前状态
    /// </summary>
    /// <param name="index">输出点索引，从0开始</param>
    bool GetOutput(int index);
    /// <summary>
    /// 读取输出点，并区分低电平与读取失败。
    /// </summary>
    MotionResult<bool> ReadOutput(int index)
        => MotionResult<bool>.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化读取接口。");
    /// <summary>
    /// 写通用输出点
    /// </summary>
    /// <param name="index">输出点索引，从0开始</param>
    /// <param name="state">true输出高电平</param>
    bool SetOutput(int index, bool state);
    /// <summary>写通用输出点，并保留失败原因。</summary>
    MotionResult SetOutputResult(int index, bool state)
        => SetOutput(index, state) ? MotionResult.Success() : MotionResult.Failure(EMotionResult.Fail, "写输出点失败。");
    #endregion

    #region 同步等待
    /// <summary>
    /// 等待轴到位（轮询方式）
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>true表示在超时前到位；超时或报警返回false</returns>
    Task<bool> WaitInPositionAsync(int axis, int timeoutMs = 5000);
    /// <summary>
    /// 等待轴到位，明确区分成功、超时、报警、急停和取消。
    /// </summary>
    Task<MotionResult> WaitInPositionResultAsync(int axis, int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MotionResult.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化等待接口。"));
    /// <summary>
    /// 等待回零完成（轮询方式）
    /// </summary>
    /// <param name="axis">轴索引，从0开始</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>true表示在超时前完成回零；超时或报警返回false</returns>
    Task<bool> WaitHomeDoneAsync(int axis, int timeoutMs = 30000);
    /// <summary>
    /// 等待回零完成，明确区分成功、超时、报警、急停和取消。
    /// </summary>
    Task<MotionResult> WaitHomeDoneResultAsync(int axis, int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MotionResult.Failure(EMotionResult.NotSupported, "当前驱动尚未实现结果化等待接口。"));
    #endregion
}
