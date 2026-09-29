using MotionControl.Core.Definitions;
using MotionControl.Runtime.Internal;
using MotionControl.Runtime.Services;

namespace MotionControl.Runtime.Vendors.Leadshine
{
    /// <summary>
    /// 雷赛控制卡控制类（基于LTDMC函数库，适用于DMC3000等脉冲系列）
    /// <para>
    /// 使用前提：
    /// 1. 目标机已安装雷赛驱动（LTDMC.dll位于系统目录，32/64位与进程一致）；
    /// 2. 轴索引即Param.Axises列表顺序，映射为板卡轴号（从0开始）；
    /// 3. 所有位置/速度以脉冲为单位与LTDMC交互（DMC3000脉冲卡无当量模式），用户单位由基类按脉冲当量换算；
    /// 4. 加减速以时间形式下发：Tacc=速度/加速度。
    /// </para>
    /// </summary>
    internal class LeadshineHelper : MotionControlBase
    {
        #region 厂商私有常量
        /// <summary>最小加减速时间（秒），防止除零或过小</summary>
        private const double MinAccDecTime = 0.005;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        private readonly ILeadshineTransport _transport;
        private readonly AxisPollingMonitor _monitor;
        private bool[] _servoOn = Array.Empty<bool>();
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeDone = Array.Empty<bool>();
        private double[] _velOverride = Array.Empty<double>(); // <=0表示未用SetSpeed覆盖

        public LeadshineHelper()
            : this(new LeadshineNativeTransport())
        {
        }

        internal LeadshineHelper(ILeadshineTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _monitor = new AxisPollingMonitor(
                MonitorIntervalMs,
                ReadMonitorSample,
                (axis, rawStatus) => RaiseAlarm(axis, $"雷赛卡报警：{EffectiveParam.Name}(轴{axis})", rawStatus),
                (axis, _) => CheckHomeDone(axis));
        }

        protected override MotionResult ValidateParameters()
        {
            var common = base.ValidateParameters();
            if (!common.IsSuccess)
                return common;
            if (EffectiveParam.CardNo > ushort.MaxValue)
                return MotionResult.Failure(EMotionResult.InvalidParam, "雷赛卡号必须在 0 到 65535 之间。");
            return MotionResult.Success();
        }

        #region 连接/断开
        protected override EMotionResult ConnectRefactor()
        {
            try
            {
                lock (_sync)
                {
                    if (_isConnected)
                        return EMotionResult.Success;

                    var openResult = _transport.Open((ushort)EffectiveParam.CardNo, AxisCount);
                    if (openResult != EMotionResult.Success)
                        return openResult;

                    _servoOn = new bool[AxisCount];
                    _homing = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _velOverride = new double[AxisCount];

                    for (int i = 0; i < AxisCount; i++)
                    {
                        if (ApplyProfile(i))
                            continue;

                        _transport.Close();
                        ResetRuntimeState();
                        return EMotionResult.Fail;
                    }

                    _monitor.Start(AxisCount);
                    return EMotionResult.Success;
                }
            }
            catch (DllNotFoundException)
            {
                // 系统中未安装雷赛驱动
                return EMotionResult.Fail;
            }
            catch (BadImageFormatException)
            {
                // DLL位数与进程位数不一致
                return EMotionResult.Fail;
            }
        }

        protected override EMotionResult DisConnectRefactor()
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.Success;

                StopAllInternal(EStopMode.Emergency);
                _monitor.Stop();
                _transport.Close();

                ResetRuntimeState();
                return EMotionResult.Success;
            }
        }

        private void ResetRuntimeState()
        {
            _servoOn = Array.Empty<bool>();
            _homing = Array.Empty<bool>();
            _homeDone = Array.Empty<bool>();
            _velOverride = Array.Empty<double>();
        }

        /// <summary>下发速度曲线（时间型加减速：Tacc=速度/加速度）</summary>
        private bool ApplyProfile(int axis)
        {
            var cfg = AxisConfig(axis);
            double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
            double velUnit = EffectiveVelocity(axis);
            double maxVel = Math.Max(1, velUnit * equiv);
            double minVel = Math.Max(1, cfg.VelLow * equiv);
            double stopVel = minVel;
            double tacc = cfg.Accel > 0 ? Math.Max(MinAccDecTime, velUnit / cfg.Accel) : MinAccDecTime;
            double tdec = cfg.Decel > 0 ? Math.Max(MinAccDecTime, velUnit / cfg.Decel) : MinAccDecTime;

            return _transport.SetProfile(axis, minVel, maxVel, tacc, tdec, stopVel);
        }
        #endregion

        #region 伺服控制
        protected override EMotionResult ServoOnRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var succeeded = _transport.SetServo(axis, true);
                if (succeeded)
                    _servoOn[axis] = true;
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var succeeded = _transport.SetServo(axis, false);
                if (succeeded)
                {
                    _servoOn[axis] = false;
                    _homing[axis] = false;
                    _homeDone[axis] = false;
                }
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;

                // 脉冲型板卡无软件清报警指令：通过伺服重新使能复位ALM；
                // 若驱动器需专用RST信号清障请改为输出RST。
                bool wasOn = _servoOn[axis];
                if (!_transport.SetServo(axis, false))
                    return EMotionResult.Fail;
                _servoOn[axis] = false;
                Thread.Sleep(100);
                if (wasOn)
                {
                    if (!_transport.SetServo(axis, true))
                        return EMotionResult.Fail;
                    _servoOn[axis] = true;
                }

                _homeDone[axis] = false;
                _homing[axis] = false;
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 运动控制
        protected override EMotionResult SetSpeedRefactor(int axis, long velPulse)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var cfg = AxisConfig(axis);
                if (cfg.PulseEquivalent <= 0)
                    return EMotionResult.InvalidParam;
                _velOverride[axis] = velPulse / cfg.PulseEquivalent;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult MoveAbsRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!CheckSoftLimit(axis, pulse))
                    return EMotionResult.InvalidParam;
                if (!ApplyProfile(axis))
                    return EMotionResult.Fail;
                return _transport.MovePosition(axis, ClampPulse(pulse), true)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                long target = _transport.GetCommandPosition(axis) + pulse;
                if (!CheckSoftLimit(axis, target))
                    return EMotionResult.InvalidParam;
                if (!ApplyProfile(axis))
                    return EMotionResult.Fail;
                return _transport.MovePosition(axis, ClampPulse(pulse), false)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!ApplyProfile(axis))
                    return EMotionResult.Fail;
                return _transport.MoveVelocity(axis, direction > 0)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                return _transport.Stop(axis, stopMode == EStopMode.Emergency)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult StopAllRefactor(EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                return StopAllInternal(stopMode)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        private bool StopAllInternal(EStopMode stopMode)
        {
            if (stopMode == EStopMode.Emergency)
            {
                // 急停直接调用整卡急停指令
                var succeeded = _transport.EmergencyStopAll();
                for (int i = 0; i < _homing.Length; i++)
                    _homing[i] = false;
                return succeeded;
            }
            var allSucceeded = true;
            for (int i = 0; i < _homing.Length; i++)
            {
                _homing[i] = false;
                allSucceeded &= _transport.Stop(i, false);
            }
            return allSucceeded;
        }

        /// <summary>运动前置检查（连接、伺服使能）</summary>
        private bool CheckReady(int axis)
        {
            return _isConnected && _servoOn[axis];
        }

        /// <summary>软限位检查</summary>
        private bool CheckSoftLimit(int axis, long pulse)
        {
            var cfg = AxisConfig(axis);
            if (!cfg.EnableSoftLimit)
                return true;
            return pulse <= ToPulse(axis, cfg.SoftLimitPositive)
                && pulse >= ToPulse(axis, cfg.SoftLimitNegative);
        }

        /// <summary>当前生效速度（用户单位/s）：SetSpeed覆盖优先，否则取配置VelHigh</summary>
        private double EffectiveVelocity(int axis)
        {
            var cfg = AxisConfig(axis);
            return _velOverride[axis] > 0 ? _velOverride[axis] : cfg.VelHigh;
        }

        private static int ClampPulse(long pulse)
            => (int)Math.Clamp(pulse, int.MinValue, int.MaxValue);
        #endregion

        #region 回零
        protected override EMotionResult HomeStartRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (!CheckReady(axis))
                    return EMotionResult.Fail;

                var cfg = AxisConfig(axis);
                switch (cfg.HomeMode)
                {
                    case EHomeMode.None:
                        return EMotionResult.InvalidParam;
                    case EHomeMode.SetCurrentAsOrigin:
                        if (!_transport.SetCurrentPosition(axis, 0))
                            return EMotionResult.Fail;
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        // 回零方向：0负向 1正向；回零速度按当量换算
                        ushort dir = cfg.HomeDirection == EDirection.Positive ? (ushort)1 : (ushort)0;
                        double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
                        ushort mode = ToLeadshineHomeMode(cfg.HomeMode);

                        // 回零偏移由板卡自动执行
                        double offset = cfg.HomeOffset;
                        int? offsetPulse = Math.Abs(offset) > double.Epsilon
                            ? ClampPulse(ToPulse(axis, offset))
                            : null;
                        if (!_transport.ConfigureHome(axis, dir, Math.Max(1, cfg.HomeVelocity * equiv), mode,
                                cfg.HomeMode == EHomeMode.LimitSwitch, offsetPulse))
                            return EMotionResult.Fail;

                        _homing[axis] = true;
                        _homeDone[axis] = false;
                        var succeeded = _transport.StartHome(axis);
                        if (!succeeded)
                            _homing[axis] = false;
                        return succeeded ? EMotionResult.Success : EMotionResult.Fail;
                }
            }
        }

        /// <summary>通用回零模式映射到雷赛回零模式值</summary>
        private static ushort ToLeadshineHomeMode(EHomeMode mode)
        {
            return mode switch
            {
                // 原点加同向EZ
                EHomeMode.ZPhase => 3,
                // 其余（原点开关/限位开关）统一一次回零，限位反找由dmc_set_home_el_return配合
                _ => 0,
            };
        }
        #endregion

        #region 状态查询
        protected override EMotionResult GetCommandPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                pulse = _transport.GetCommandPosition(axis);
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                pulse = _transport.GetEncoderPosition(axis);
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                uint ioStatus = _transport.GetAxisIoStatus(axis);
                // dmc_check_done返回0表示运动中，1表示已停止(到位)
                bool done = _transport.CheckDone(axis) == 1;
                long packed = (done ? 1L << 1 : 1L << 0); // 高32位自定义：bit0=Moving bit1=InPosition
                raw = ioStatus | (packed << 32);
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 将雷赛状态字映射为通用轴状态标志
        /// </summary>
        protected override EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            uint ioStatus = unchecked((uint)raw);
            long packed = unchecked((int)(raw >> 32));

            var flags = EAxisStatusFlag.None;
            if ((ioStatus & LTDMC.LTS_IO_ALM) != 0)
                flags |= EAxisStatusFlag.Alarm;
            if ((ioStatus & LTDMC.LTS_IO_PEL) != 0)
                flags |= EAxisStatusFlag.PositiveLimit;
            if ((ioStatus & LTDMC.LTS_IO_MEL) != 0)
                flags |= EAxisStatusFlag.NegativeLimit;
            if ((ioStatus & LTDMC.LTS_IO_EMG) != 0)
                flags |= EAxisStatusFlag.EmergencyStop;
            if ((packed & (1L << 0)) != 0)
                flags |= EAxisStatusFlag.Moving;
            if ((packed & (1L << 1)) != 0)
                flags |= EAxisStatusFlag.InPosition;

            lock (_sync)
            {
                if (_servoOn[axis])
                    flags |= EAxisStatusFlag.Enabled;
                if (_homeDone[axis])
                    flags |= EAxisStatusFlag.Homed;
            }
            return flags;
        }
        #endregion

        #region IO控制
        protected override EMotionResult GetInputRefactor(int index, out bool state)
        {
            return ReadBit(_transport.ReadInput, index, out state);
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            return ReadBit(_transport.ReadOutput, index, out state);
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            if (index < 0 || index > ushort.MaxValue)
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                return _transport.WriteOutput(index, state)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        private EMotionResult ReadBit(Func<int, short> readFunc, int index, out bool state)
        {
            state = false;
            if (index < 0 || index > ushort.MaxValue)
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var level = readFunc(index);
                if (level < 0)
                    return EMotionResult.Fail;
                state = level > 0;
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 后台监控（报警事件 + 回零完成检测）
        private AxisMonitorSample? ReadMonitorSample(int axis)
        {
            lock (_sync)
            {
                if ((uint)axis >= (uint)_servoOn.Length)
                    return null;

                uint status = _transport.GetAxisIoStatus(axis);
                return new AxisMonitorSample(status, (status & LTDMC.LTS_IO_ALM) != 0);
            }
        }

        /// <summary>回零完成检测：dmc_get_home_result返回1表示回零完成</summary>
        private void CheckHomeDone(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected || (uint)axis >= (uint)_homing.Length || !_homing[axis])
                    return;
                if (!_transport.TryGetHomeResult(axis, out var state))
                    return;
                if (state == 1)
                {
                    _homing[axis] = false;
                    _homeDone[axis] = true;
                }
            }
        }
        #endregion
    }
}


