using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;

namespace MotionControl.Runtime.Vendors.Simulation
{
    /// <summary>
    /// 仿真运动控制卡（无硬件调试用）
    /// <para>内部以脉冲为单位模拟轴运动，位置按配置速度随时间插补逼近目标，到位后置InPosition/Homed标志</para>
    /// </summary>
    internal class SimulationMotionCard : MotionControlBase, ISimulationMotionCard
    {
        private const int IoCount = 16;
        private const int TickIntervalMs = 20;

        private readonly object _sync = new object();
        private System.Threading.Timer? _timer;
        private long[] _cmdPos = Array.Empty<long>();
        private long[] _encPos = Array.Empty<long>();
        private long[] _target = Array.Empty<long>();
        private long[] _vel = Array.Empty<long>();      // 运行速度（SetSpeed可修改）
        private long[] _runVel = Array.Empty<long>();   // 当前本次运动实际使用的速度
        private double[] _fractionalPulse = Array.Empty<double>(); // 跨tick保留不足1脉冲的位移
        private EAxisStatusFlag[] _flags = Array.Empty<EAxisStatusFlag>();
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _inputs = new bool[IoCount];
        private bool[] _outputs = new bool[IoCount];
        private bool _opened;

        #region 连接/断开
        protected override EMotionResult ConnectRefactor()
        {
            lock (_sync)
            {
                if (_opened)
                    return EMotionResult.Success;
                if (AxisCount <= 0)
                    return EMotionResult.InvalidParam;

                _cmdPos = new long[AxisCount];
                _encPos = new long[AxisCount];
                _target = new long[AxisCount];
                _vel = new long[AxisCount];
                _runVel = new long[AxisCount];
                _fractionalPulse = new double[AxisCount];
                _flags = new EAxisStatusFlag[AxisCount];
                _homing = new bool[AxisCount];

                for (int i = 0; i < AxisCount; i++)
                    _vel[i] = ToPulse(i, AxisConfig(i).VelHigh);

                _timer = new System.Threading.Timer(OnTick, null, TickIntervalMs, TickIntervalMs);
                _opened = true;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult DisConnectRefactor()
        {
            lock (_sync)
            {
                if (!_opened)
                    return EMotionResult.Success;
                StopAllInternal(EStopMode.Emergency);
                _timer?.Dispose();
                _timer = null;
                _opened = false;
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 伺服控制
        protected override EMotionResult ServoOnRefactor(int axis)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                _flags[axis] |= EAxisStatusFlag.Enabled;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                StopAxisInternal(axis, EStopMode.Emergency);
                _flags[axis] &= ~EAxisStatusFlag.Enabled;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                _flags[axis] &= ~(EAxisStatusFlag.Alarm | EAxisStatusFlag.PositiveLimit
                                  | EAxisStatusFlag.NegativeLimit | EAxisStatusFlag.EmergencyStop);
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 运动控制
        protected override EMotionResult SetSpeedRefactor(int axis, long velPulse)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                _vel[axis] = velPulse;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult MoveAbsRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                if ((_flags[axis] & EAxisStatusFlag.Enabled) == 0)
                    return EMotionResult.Fail;
                if ((_flags[axis] & (EAxisStatusFlag.Alarm | EAxisStatusFlag.EmergencyStop)) != 0)
                    return EMotionResult.Fail;

                var cfg = AxisConfig(axis);
                if (cfg.EnableSoftLimit && (pulse > ToPulse(axis, cfg.SoftLimitPositive) || pulse < ToPulse(axis, cfg.SoftLimitNegative)))
                    return EMotionResult.InvalidParam;

                StartMotion(axis, pulse, _vel[axis], homing: false);
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                long target;
                try
                {
                    target = checked(_cmdPos[axis] + pulse);
                }
                catch (OverflowException)
                {
                    return EMotionResult.InvalidParam;
                }
                return MoveAbsRefactor(axis, target);
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                if ((_flags[axis] & EAxisStatusFlag.Enabled) == 0 ||
                    (_flags[axis] & (EAxisStatusFlag.Alarm | EAxisStatusFlag.EmergencyStop)) != 0)
                    return EMotionResult.Fail;

                // 点动目标设为极远端，持续运动直到Stop
                var cfg = AxisConfig(axis);
                long farTarget = cfg.EnableSoftLimit
                    ? ToPulse(axis, direction > 0 ? cfg.SoftLimitPositive : cfg.SoftLimitNegative)
                    : direction > 0 ? long.MaxValue / 4 : long.MinValue / 4;
                StartMotion(axis, farTarget, _vel[axis], homing: false);
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                StopAxisInternal(axis, stopMode);
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult StopAllRefactor(EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                StopAllInternal(stopMode);
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 启动运动（调用前必须已持有_sync锁）
        /// </summary>
        private void StartMotion(int axis, long target, long vel, bool homing)
        {
            _target[axis] = target;
            _runVel[axis] = vel > 0 ? vel : 1;
            _fractionalPulse[axis] = 0;
            _homing[axis] = homing;
            _flags[axis] &= ~EAxisStatusFlag.InPosition;
            _flags[axis] |= EAxisStatusFlag.Moving;
            if (!homing)
                _flags[axis] &= ~EAxisStatusFlag.Homed;
        }

        private void StopAxisInternal(int axis, EStopMode stopMode)
        {
            _target[axis] = _cmdPos[axis];
            _fractionalPulse[axis] = 0;
            _homing[axis] = false;
            _flags[axis] &= ~EAxisStatusFlag.Moving;
            if (stopMode == EStopMode.Emergency)
                _flags[axis] |= EAxisStatusFlag.EmergencyStop | EAxisStatusFlag.InPosition;
            else
                _flags[axis] |= EAxisStatusFlag.InPosition;
        }

        private void StopAllInternal(EStopMode stopMode)
        {
            for (int i = 0; i < AxisCount; i++)
                StopAxisInternal(i, stopMode);
        }
        #endregion

        #region 回零
        protected override EMotionResult HomeStartRefactor(int axis)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                if ((_flags[axis] & EAxisStatusFlag.Enabled) == 0 ||
                    (_flags[axis] & (EAxisStatusFlag.Alarm | EAxisStatusFlag.EmergencyStop)) != 0)
                    return EMotionResult.Fail;

                var cfg = AxisConfig(axis);
                switch (cfg.HomeMode)
                {
                    case EHomeMode.None:
                        return EMotionResult.InvalidParam;
                    case EHomeMode.SetCurrentAsOrigin:
                        _cmdPos[axis] = 0;
                        _encPos[axis] = 0;
                        _flags[axis] |= EAxisStatusFlag.Homed | EAxisStatusFlag.InPosition;
                        return EMotionResult.Success;
                    default:
                        // 原点开关/Z相/限位回零：统一模拟为向原点方向运动到偏移位置
                        long homeTarget = ToPulse(axis, cfg.HomeOffset);
                        long homeVel = ToPulse(axis, cfg.HomeVelocity);
                        StartMotion(axis, homeTarget, homeVel, homing: true);
                        return EMotionResult.Success;
                }
            }
        }
        #endregion

        #region 状态查询
        protected override EMotionResult GetCommandPositionRefactor(int axis, out long pulse)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                {
                    pulse = 0;
                    return EMotionResult.NotConnected;
                }
                pulse = _cmdPos[axis];
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                {
                    pulse = 0;
                    return EMotionResult.NotConnected;
                }
                pulse = _encPos[axis];
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                {
                    raw = 0;
                    return EMotionResult.NotConnected;
                }
                raw = (long)_flags[axis];
                return EMotionResult.Success;
            }
        }
        #endregion

        #region IO控制
        protected override EMotionResult GetInputRefactor(int index, out bool state)
        {
            if (index < 0 || index >= IoCount)
            {
                state = false;
                return EMotionResult.InvalidParam;
            }
            lock (_sync)
            {
                if (!CheckOpened())
                {
                    state = false;
                    return EMotionResult.NotConnected;
                }
                state = _inputs[index];
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            if (index < 0 || index >= IoCount)
            {
                state = false;
                return EMotionResult.InvalidParam;
            }
            lock (_sync)
            {
                if (!CheckOpened())
                {
                    state = false;
                    return EMotionResult.NotConnected;
                }
                state = _outputs[index];
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            lock (_sync)
            {
                if (!CheckOpened())
                    return EMotionResult.NotConnected;
                if (index < 0 || index >= IoCount)
                    return EMotionResult.InvalidParam;
                _outputs[index] = state;
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 运动仿真
        private void OnTick(object? state)
        {
            lock (_sync)
            {
                if (!_opened)
                    return;
                double dt = TickIntervalMs / 1000.0;
                for (int i = 0; i < AxisCount; i++)
                {
                    if ((_flags[i] & EAxisStatusFlag.Moving) == 0)
                        continue;

                    double exactStep = _runVel[i] * dt + _fractionalPulse[i];
                    long step = (long)Math.Floor(exactStep);
                    _fractionalPulse[i] = exactStep - step;
                    if (step <= 0)
                        continue;

                    decimal distance = Math.Abs((decimal)_target[i] - _cmdPos[i]);
                    if (distance <= step)
                    {
                        _cmdPos[i] = _target[i];
                        _encPos[i] = _target[i];
                        _fractionalPulse[i] = 0;
                        _flags[i] &= ~EAxisStatusFlag.Moving;
                        _flags[i] |= EAxisStatusFlag.InPosition;
                        if (_homing[i])
                        {
                            _homing[i] = false;
                            _flags[i] |= EAxisStatusFlag.Homed;
                        }
                    }
                    else
                    {
                        _cmdPos[i] += _target[i] > _cmdPos[i] ? step : -step;
                        _encPos[i] = _cmdPos[i];
                    }
                }
            }
        }

        private bool CheckOpened()
        {
            if (!_opened)
                return false;
            if (AxisCount != _cmdPos.Length)
                throw new InvalidOperationException("轴数量与已连接的卡不一致，请重新Connect");
            return true;
        }
        #endregion

        #region 仿真辅助（供测试/调试）
        /// <summary>
        /// 模拟设置输入点状态（如模拟限位开关、传感器信号）
        /// </summary>
        public void SetSimInput(int index, bool state)
        {
            lock (_sync)
            {
                if (index >= 0 && index < IoCount)
                    _inputs[index] = state;
            }
        }

        /// <summary>
        /// 模拟触发轴报警并抛出AlarmOccurred事件
        /// </summary>
        public void TriggerSimAlarm(int axis)
        {
            lock (_sync)
            {
                if (!_opened || axis < 0 || axis >= AxisCount)
                    return;
                _flags[axis] |= EAxisStatusFlag.Alarm;
                RaiseAlarm(axis, $"仿真报警：轴{axis}", (long)_flags[axis]);
            }
        }
        #endregion
    }
}


