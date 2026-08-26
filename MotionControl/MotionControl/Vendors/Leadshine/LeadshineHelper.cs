using MotionControl.Definition;

namespace MotionControl.Vendors.Leadshine
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
        /// <summary>SEVON信号低电平有效：写0为使能伺服</summary>
        private const ushort LTS_SEVON_ENABLE = 0;
        /// <summary>SEVON信号低电平有效：写1为失能伺服</summary>
        private const ushort LTS_SEVON_DISABLE = 1;
        /// <summary>dmc_pmove位置模式：相对</summary>
        private const ushort LTS_POS_RELATIVE = 0;
        /// <summary>dmc_pmove位置模式：绝对</summary>
        private const ushort LTS_POS_ABSOLUTE = 1;
        /// <summary>dmc_stop停止方式：减速停止</summary>
        private const ushort LTS_STOP_DECELERATE = 0;
        /// <summary>dmc_stop停止方式：立即停止</summary>
        private const ushort LTS_STOP_EMERGENCY = 1;
        /// <summary>dmc_vmove方向：正方向</summary>
        private const ushort LTS_DIR_POSITIVE = 0;
        /// <summary>dmc_vmove方向：负方向</summary>
        private const ushort LTS_DIR_NEGATIVE = 1;
        /// <summary>回零遇限位反找使能</summary>
        private const ushort LTS_HOME_EL_RETURN_ENABLE = 1;
        /// <summary>最小加减速时间（秒），防止除零或过小</summary>
        private const double MinAccDecTime = 0.005;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        /// <summary>已调用dmc_board_init的实例计数（库初始化为进程级全局操作）</summary>
        private static int _initCount;

        private CancellationTokenSource? _monitorCts;
        private bool[] _servoOn = Array.Empty<bool>();
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeDone = Array.Empty<bool>();
        private double[] _velOverride = Array.Empty<double>(); // <=0表示未用SetSpeed覆盖

        #region 连接/断开
        protected override EMotionResult ConnectRefactor()
        {
            try
            {
                lock (_sync)
                {
                    if (_isConnected)
                        return EMotionResult.Success;

                    // 库初始化（进程级一次）
                    if (_initCount == 0)
                    {
                        short cards = LTDMC.dmc_board_init();
                        if (cards <= 0)
                            return EMotionResult.Fail;
                    }

                    // 校验卡号存在
                    ushort cardNum = 0;
                    var typeList = new uint[16];
                    var idList = new ushort[16];
                    if (LTDMC.dmc_get_CardInfList(ref cardNum, typeList, idList) != 0
                        || !idList.Take(cardNum).Contains((ushort)Param.CardNo))
                    {
                        TryFinalClose();
                        return EMotionResult.Fail;
                    }

                    // 校验轴数
                    uint totalAxes = 0;
                    if (LTDMC.dmc_get_total_axes((ushort)Param.CardNo, ref totalAxes) != 0
                        || AxisCount <= 0 || AxisCount > totalAxes)
                    {
                        TryFinalClose();
                        return EMotionResult.InvalidParam;
                    }

                    _servoOn = new bool[AxisCount];
                    _homing = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _velOverride = new double[AxisCount];

                    for (int i = 0; i < AxisCount; i++)
                        ApplyProfile(i);

                    StartMonitor();
                    _initCount++;
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
                _monitorCts?.Cancel();
                _monitorCts = null;

                _initCount--;
                TryFinalClose();

                _servoOn = Array.Empty<bool>();
                return EMotionResult.Success;
            }
        }

        /// <summary>本进程最后一张卡断开时关闭整个LTDMC库</summary>
        private void TryFinalClose()
        {
            if (_initCount <= 0)
            {
                _initCount = 0;
                LTDMC.dmc_board_close();
            }
        }

        /// <summary>下发速度曲线（时间型加减速：Tacc=速度/加速度）</summary>
        private void ApplyProfile(int axis)
        {
            var cfg = AxisConfig(axis);
            double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
            double velUnit = EffectiveVelocity(axis);
            double maxVel = Math.Max(1, velUnit * equiv);
            double minVel = Math.Max(1, cfg.VelLow * equiv);
            double stopVel = minVel;
            double tacc = cfg.Accel > 0 ? Math.Max(MinAccDecTime, velUnit / cfg.Accel) : MinAccDecTime;
            double tdec = cfg.Decel > 0 ? Math.Max(MinAccDecTime, velUnit / cfg.Decel) : MinAccDecTime;

            LTDMC.dmc_set_profile((ushort)Param.CardNo, (ushort)axis, minVel, maxVel, tacc, tdec, stopVel);
        }
        #endregion

        #region 伺服控制
        protected override EMotionResult ServoOnRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = LTDMC.dmc_write_sevon_pin((ushort)Param.CardNo, (ushort)axis, LTS_SEVON_ENABLE);
                if (ret == 0)
                    _servoOn[axis] = true;
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = LTDMC.dmc_write_sevon_pin((ushort)Param.CardNo, (ushort)axis, LTS_SEVON_DISABLE);
                if (ret == 0)
                {
                    _servoOn[axis] = false;
                    _homing[axis] = false;
                    _homeDone[axis] = false;
                }
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
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
                LTDMC.dmc_write_sevon_pin((ushort)Param.CardNo, (ushort)axis, LTS_SEVON_DISABLE);
                Thread.Sleep(100);
                if (wasOn)
                    LTDMC.dmc_write_sevon_pin((ushort)Param.CardNo, (ushort)axis, LTS_SEVON_ENABLE);

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
                ApplyProfile(axis);
                var ret = LTDMC.dmc_pmove((ushort)Param.CardNo, (ushort)axis, ClampPulse(pulse), LTS_POS_ABSOLUTE);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                long target = LTDMC.dmc_get_position((ushort)Param.CardNo, (ushort)axis) + pulse;
                if (!CheckSoftLimit(axis, target))
                    return EMotionResult.InvalidParam;
                ApplyProfile(axis);
                var ret = LTDMC.dmc_pmove((ushort)Param.CardNo, (ushort)axis, ClampPulse(pulse), LTS_POS_RELATIVE);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                ApplyProfile(axis);
                var dir = direction > 0 ? LTS_DIR_POSITIVE : LTS_DIR_NEGATIVE;
                var ret = LTDMC.dmc_vmove((ushort)Param.CardNo, (ushort)axis, dir);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                var mode = stopMode == EStopMode.Emergency ? LTS_STOP_EMERGENCY : LTS_STOP_DECELERATE;
                var ret = LTDMC.dmc_stop((ushort)Param.CardNo, (ushort)axis, mode);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult StopAllRefactor(EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                StopAllInternal(stopMode);
                return EMotionResult.Success;
            }
        }

        private void StopAllInternal(EStopMode stopMode)
        {
            if (stopMode == EStopMode.Emergency)
            {
                // 急停直接调用整卡急停指令
                LTDMC.dmc_emg_stop((ushort)Param.CardNo);
                for (int i = 0; i < _homing.Length; i++)
                    _homing[i] = false;
                return;
            }
            for (int i = 0; i < _homing.Length; i++)
            {
                _homing[i] = false;
                LTDMC.dmc_stop((ushort)Param.CardNo, (ushort)i, LTS_STOP_DECELERATE);
            }
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
                        LTDMC.dmc_set_position((ushort)Param.CardNo, (ushort)axis, 0);
                        LTDMC.dmc_set_encoder((ushort)Param.CardNo, (ushort)axis, 0);
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        // 回零方向：0负向 1正向；回零速度按当量换算
                        ushort dir = cfg.HomeDirection == EDirection.Positive ? (ushort)1 : (ushort)0;
                        double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
                        ushort mode = ToLeadshineHomeMode(cfg.HomeMode);

                        LTDMC.dmc_set_homemode((ushort)Param.CardNo, (ushort)axis, dir,
                            Math.Max(1, cfg.HomeVelocity * equiv), mode, 0);
                        LTDMC.dmc_set_home_el_return((ushort)Param.CardNo, (ushort)axis,
                            cfg.HomeMode == EHomeMode.LimitSwitch ? LTS_HOME_EL_RETURN_ENABLE : (ushort)0);

                        // 回零偏移由板卡自动执行
                        double offset = cfg.HomeOffset;
                        if (Math.Abs(offset) > double.Epsilon)
                            LTDMC.dmc_set_home_position((ushort)Param.CardNo, (ushort)axis, 1, ClampPulse(ToPulse(axis, offset)));

                        _homing[axis] = true;
                        _homeDone[axis] = false;
                        var ret = LTDMC.dmc_home_move((ushort)Param.CardNo, (ushort)axis);
                        if (ret != 0)
                            _homing[axis] = false;
                        return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
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
                pulse = LTDMC.dmc_get_position((ushort)Param.CardNo, (ushort)axis);
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
                pulse = LTDMC.dmc_get_encoder((ushort)Param.CardNo, (ushort)axis);
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
                uint ioStatus = LTDMC.dmc_axis_io_status((ushort)Param.CardNo, (ushort)axis);
                // dmc_check_done返回0表示运动中，1表示已停止(到位)
                bool done = LTDMC.dmc_check_done((ushort)Param.CardNo, (ushort)axis) == 1;
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
            return ReadBit(LTDMC.dmc_read_inbit, index, out state);
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            return ReadBit(LTDMC.dmc_read_outbit, index, out state);
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            if (index < 0 || index > ushort.MaxValue)
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = LTDMC.dmc_write_outbit((ushort)Param.CardNo, (ushort)index, (ushort)(state ? 1 : 0));
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        private EMotionResult ReadBit(Func<ushort, ushort, short> readFunc, int index, out bool state)
        {
            state = false;
            if (index < 0 || index > ushort.MaxValue)
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var level = readFunc((ushort)Param.CardNo, (ushort)index);
                if (level < 0)
                    return EMotionResult.Fail;
                state = level > 0;
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 后台监控（报警事件 + 回零完成检测）
        /// <summary>
        /// 启动后台监控线程：轮询各轴IO状态字，报警上升沿抛出AlarmOccurred；检测回零完成后置Homed
        /// </summary>
        private void StartMonitor()
        {
            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;
            _ = Task.Run(() =>
            {
                var lastAlm = new bool[_servoOn.Length];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < _servoOn.Length; i++)
                        {
                            uint ioStatus = LTDMC.dmc_axis_io_status((ushort)Param.CardNo, (ushort)i);
                            bool alm = (ioStatus & LTDMC.LTS_IO_ALM) != 0;
                            if (alm && !lastAlm[i])
                                RaiseAlarm(i, $"雷赛卡报警：{Param.Name}(轴{i})", ioStatus);
                            lastAlm[i] = alm;

                            CheckHomeDone(i);
                        }
                    }
                    catch
                    {
                        // 监控线程不允许因瞬时异常退出
                    }
                    token.WaitHandle.WaitOne(MonitorIntervalMs);
                }
            }, token);
        }

        /// <summary>回零完成检测：dmc_get_home_result返回1表示回零完成</summary>
        private void CheckHomeDone(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected || !_homing[axis])
                    return;
                ushort state = 0;
                if (LTDMC.dmc_get_home_result((ushort)Param.CardNo, (ushort)axis, ref state) != 0)
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
