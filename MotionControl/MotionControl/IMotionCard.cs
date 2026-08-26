using Device;
using MotionControl.Definition;

namespace MotionControl
{
    /// <summary>
    /// 运动控制卡接口
    /// </summary>
    public interface IMotionCard : IDevice
    {
        /// <summary>
        /// 运动控制卡参数
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

        #region 伺服控制
        /// <summary>
        /// 伺服使能
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        bool ServoOn(int axis);
        /// <summary>
        /// 伺服失能
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        bool ServoOff(int axis);
        /// <summary>
        /// 清除轴状态（报警、限位等）
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        bool ClearAxisStatus(int axis);
        #endregion

        #region 运动控制
        /// <summary>
        /// 设置轴运行速度
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <param name="velocity">速度（用户单位/s）</param>
        bool SetSpeed(int axis, double velocity);
        /// <summary>
        /// 绝对运动到目标位置
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <param name="position">目标位置（用户单位）</param>
        bool MoveAbs(int axis, double position);
        /// <summary>
        /// 相对当前位置运动
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <param name="distance">运动距离（用户单位，负值表示负方向）</param>
        bool MoveRel(int axis, double distance);
        /// <summary>
        /// 开始点动（持续运动直到Stop）
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <param name="direction">点动方向</param>
        bool JogStart(int axis, EDirection direction);
        /// <summary>
        /// 停止单轴
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <param name="stopMode">停止方式，默认减速停</param>
        bool Stop(int axis, EStopMode stopMode = EStopMode.Decelerate);
        /// <summary>
        /// 停止本卡所有轴
        /// </summary>
        /// <param name="stopMode">停止方式，默认急停</param>
        bool StopAll(EStopMode stopMode = EStopMode.Emergency);
        #endregion

        #region 回零
        /// <summary>
        /// 启动回零
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        bool StartHome(int axis);
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
        /// 获取编码器位置（反馈位置）
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <returns>位置（用户单位），失败返回0</returns>
        double GetEncoderPosition(int axis);
        /// <summary>
        /// 获取轴完整状态
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        AxisStatus GetAxisStatus(int axis);
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
        /// 读取通用输出点当前状态
        /// </summary>
        /// <param name="index">输出点索引，从0开始</param>
        bool GetOutput(int index);
        /// <summary>
        /// 写通用输出点
        /// </summary>
        /// <param name="index">输出点索引，从0开始</param>
        /// <param name="state">true输出高电平</param>
        bool SetOutput(int index, bool state);
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
        /// 等待回零完成（轮询方式）
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>true表示在超时前完成回零；超时或报警返回false</returns>
        Task<bool> WaitHomeDoneAsync(int axis, int timeoutMs = 30000);
        #endregion
    }
}
