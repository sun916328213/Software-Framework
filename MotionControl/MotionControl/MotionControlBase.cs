using MotionControl.Definition;

namespace MotionControl
{
    /// <summary>
    /// 运动控制卡抽象基类（模板方法模式）
    /// <para>公共方法负责单位换算与结果判断，厂商实现只需重写各Refactor方法（操作对象为脉冲量）</para>
    /// </summary>
    public abstract class MotionControlBase : IMotionCard
    {
        internal bool _isConnected;
        /// <inheritdoc/>
        public bool IsConnected => _isConnected;

        /// <inheritdoc/>
        public virtual string Name { get => Param.Name; set => Param.Name = value; }

        /// <inheritdoc/>
        public virtual MotionCardParam Param { get; set; } = new MotionCardParam();

        /// <inheritdoc/>
        public int AxisCount => Param?.Axises?.Count ?? 0;

        /// <inheritdoc/>
        public event EventHandler<AxisAlarmEventArgs>? AlarmOccurred;

        /// <summary>
        /// 触发轴报警事件，供具体实现调用
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="message">报警信息</param>
        /// <param name="rawStatus">原始状态字</param>
        protected void RaiseAlarm(int axis, string message, long rawStatus)
        {
            AlarmOccurred?.Invoke(this, new AxisAlarmEventArgs(axis, message, rawStatus));
        }

        #region 单位换算
        /// <summary>
        /// 获取指定轴参数（含范围校验）
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        protected AxisParam AxisConfig(int axis)
        {
            if (Param?.Axises == null || axis < 0 || axis >= Param.Axises.Count)
                throw new ArgumentOutOfRangeException(nameof(axis), $"轴索引 {axis} 超出范围（共 {AxisCount} 轴）");
            return Param.Axises[axis];
        }

        /// <summary>
        /// 用户单位转脉冲数
        /// </summary>
        protected long ToPulse(int axis, double unit) => (long)Math.Round(unit * AxisConfig(axis).PulseEquivalent);

        /// <summary>
        /// 脉冲数转用户单位
        /// </summary>
        protected double ToUnit(int axis, long pulse) => pulse / AxisConfig(axis).PulseEquivalent;
        #endregion

        #region 连接/断开
        /// <inheritdoc/>
        public bool Connect()
        {
            var iRet = ConnectRefactor();
            _isConnected = iRet == EMotionResult.Success;
            return _isConnected;
        }
        /// <summary>
        /// 连接控制卡（打开卡）
        /// </summary>
        protected abstract EMotionResult ConnectRefactor();

        /// <inheritdoc/>
        public bool DisConnect()
        {
            var iRet = DisConnectRefactor();
            _isConnected = iRet != EMotionResult.Success;
            return !_isConnected;
        }
        /// <summary>
        /// 断开控制卡（关闭卡）
        /// </summary>
        protected abstract EMotionResult DisConnectRefactor();
        #endregion

        #region 伺服控制
        /// <inheritdoc/>
        public bool ServoOn(int axis)
        {
            return ServoOnRefactor(axis) == EMotionResult.Success;
        }
        /// <summary>
        /// 伺服使能
        /// </summary>
        protected abstract EMotionResult ServoOnRefactor(int axis);

        /// <inheritdoc/>
        public bool ServoOff(int axis)
        {
            return ServoOffRefactor(axis) == EMotionResult.Success;
        }
        /// <summary>
        /// 伺服失能
        /// </summary>
        protected abstract EMotionResult ServoOffRefactor(int axis);

        /// <inheritdoc/>
        public bool ClearAxisStatus(int axis)
        {
            return ClearAxisStatusRefactor(axis) == EMotionResult.Success;
        }
        /// <summary>
        /// 清除轴状态（报警、限位等）
        /// </summary>
        protected abstract EMotionResult ClearAxisStatusRefactor(int axis);
        #endregion

        #region 运动控制
        /// <inheritdoc/>
        public bool SetSpeed(int axis, double velocity)
        {
            if (velocity <= 0)
                return false;
            return SetSpeedRefactor(axis, ToPulse(axis, velocity)) == EMotionResult.Success;
        }
        /// <summary>
        /// 设置轴运行速度
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="velPulse">速度（脉冲/s）</param>
        protected abstract EMotionResult SetSpeedRefactor(int axis, long velPulse);

        /// <inheritdoc/>
        public bool MoveAbs(int axis, double position)
        {
            return MoveAbsRefactor(axis, ToPulse(axis, position)) == EMotionResult.Success;
        }
        /// <summary>
        /// 绝对运动到目标位置
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">目标位置（脉冲）</param>
        protected abstract EMotionResult MoveAbsRefactor(int axis, long pulse);

        /// <inheritdoc/>
        public bool MoveRel(int axis, double distance)
        {
            return MoveRelRefactor(axis, ToPulse(axis, distance)) == EMotionResult.Success;
        }
        /// <summary>
        /// 相对当前位置运动
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">运动距离（脉冲，负值表示负方向）</param>
        protected abstract EMotionResult MoveRelRefactor(int axis, long pulse);

        /// <inheritdoc/>
        public bool JogStart(int axis, EDirection direction)
        {
            return JogStartRefactor(axis, (short)direction) == EMotionResult.Success;
        }
        /// <summary>
        /// 开始点动
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="direction">方向：-1负向 1正向</param>
        protected abstract EMotionResult JogStartRefactor(int axis, short direction);

        /// <inheritdoc/>
        public bool Stop(int axis, EStopMode stopMode = EStopMode.Decelerate)
        {
            return StopAxisRefactor(axis, stopMode) == EMotionResult.Success;
        }
        /// <summary>
        /// 停止单轴
        /// </summary>
        protected abstract EMotionResult StopAxisRefactor(int axis, EStopMode stopMode);

        /// <inheritdoc/>
        public bool StopAll(EStopMode stopMode = EStopMode.Emergency)
        {
            return StopAllRefactor(stopMode) == EMotionResult.Success;
        }
        /// <summary>
        /// 停止本卡所有轴
        /// </summary>
        protected abstract EMotionResult StopAllRefactor(EStopMode stopMode);
        #endregion

        #region 回零
        /// <inheritdoc/>
        public bool StartHome(int axis)
        {
            return HomeStartRefactor(axis) == EMotionResult.Success;
        }
        /// <summary>
        /// 启动回零
        /// </summary>
        protected abstract EMotionResult HomeStartRefactor(int axis);
        #endregion

        #region 状态查询
        /// <inheritdoc/>
        public double GetCommandPosition(int axis)
        {
            AxisConfig(axis);
            var iRet = GetCommandPositionRefactor(axis, out long pulse);
            return iRet == EMotionResult.Success ? ToUnit(axis, pulse) : 0;
        }
        /// <summary>
        /// 获取指令位置
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">指令位置（脉冲）</param>
        protected abstract EMotionResult GetCommandPositionRefactor(int axis, out long pulse);

        /// <inheritdoc/>
        public double GetEncoderPosition(int axis)
        {
            AxisConfig(axis);
            var iRet = GetEncoderPositionRefactor(axis, out long pulse);
            return iRet == EMotionResult.Success ? ToUnit(axis, pulse) : 0;
        }
        /// <summary>
        /// 获取编码器位置
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">编码器位置（脉冲）</param>
        protected abstract EMotionResult GetEncoderPositionRefactor(int axis, out long pulse);

        /// <inheritdoc/>
        public AxisStatus GetAxisStatus(int axis)
        {
            AxisConfig(axis);
            var iRet = GetAxisStatusRefactor(axis, out long raw);
            if (iRet != EMotionResult.Success)
                return new AxisStatus { Axis = axis };
            return new AxisStatus { Axis = axis, Raw = raw, Flags = ParseAxisFlags(axis, raw) };
        }
        /// <summary>
        /// 获取轴原始状态字
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="raw">原始状态字</param>
        protected abstract EMotionResult GetAxisStatusRefactor(int axis, out long raw);

        /// <summary>
        /// 将厂商原始状态字映射为通用状态标志，默认按EAxisStatusFlag位定义解析，厂商可重写
        /// </summary>
        protected virtual EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            return (EAxisStatusFlag)raw;
        }

        /// <inheritdoc/>
        public bool IsMoving(int axis)
        {
            return GetAxisStatus(axis).Moving;
        }

        /// <inheritdoc/>
        public bool IsInPosition(int axis)
        {
            return GetAxisStatus(axis).InPosition;
        }

        /// <inheritdoc/>
        public bool IsHomeDone(int axis)
        {
            return GetAxisStatus(axis).Homed;
        }
        #endregion

        #region IO控制
        /// <inheritdoc/>
        public bool GetInput(int index)
        {
            var iRet = GetInputRefactor(index, out bool state);
            return iRet == EMotionResult.Success && state;
        }
        /// <summary>
        /// 读取通用输入点
        /// </summary>
        protected abstract EMotionResult GetInputRefactor(int index, out bool state);

        /// <inheritdoc/>
        public bool GetOutput(int index)
        {
            var iRet = GetOutputRefactor(index, out bool state);
            return iRet == EMotionResult.Success && state;
        }
        /// <summary>
        /// 读取通用输出点当前状态
        /// </summary>
        protected abstract EMotionResult GetOutputRefactor(int index, out bool state);

        /// <inheritdoc/>
        public bool SetOutput(int index, bool state)
        {
            return SetOutputRefactor(index, state) == EMotionResult.Success;
        }
        /// <summary>
        /// 写通用输出点
        /// </summary>
        protected abstract EMotionResult SetOutputRefactor(int index, bool state);
        #endregion

        #region 同步等待
        /// <inheritdoc/>
        public async Task<bool> WaitInPositionAsync(int axis, int timeoutMs = 5000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var status = GetAxisStatus(axis);
                if (status.Alarm || status.EmergencyStop)
                    return false;
                if (!status.Moving && status.InPosition)
                    return true;
                await Task.Delay(10);
            }
            return false;
        }

        /// <inheritdoc/>
        public async Task<bool> WaitHomeDoneAsync(int axis, int timeoutMs = 30000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var status = GetAxisStatus(axis);
                if (status.Alarm || status.EmergencyStop)
                    return false;
                if (status.Homed && !status.Moving)
                    return true;
                await Task.Delay(10);
            }
            return false;
        }
        #endregion
    }
}
