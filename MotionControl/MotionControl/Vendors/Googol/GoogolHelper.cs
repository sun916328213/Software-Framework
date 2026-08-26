using MotionControl.Definition;

namespace MotionControl.Vendors.Googol
{
    /// <summary>
    /// 固高GTS系列控制卡控制类（基于gts.dll）
    /// <para>
    /// 使用前提：
    /// 1. 目标机已安装固高驱动（gts.dll位于系统目录，位数与进程一致）；
    /// 2. 建议通过MotionCardParam.ConfigFile指定gts.cfg配置文件（固高配置工具生成），连接时自动下载；
    /// 3. 轴索引即Param.Axises列表顺序（0基），内部映射为固高轴号=索引+1；
    /// 4. 位置/速度以脉冲为单位交互，用户单位由基类按脉冲当量换算。
    /// </para>
    /// </summary>
    internal class GoogolHelper : MotionControlBase
    {
        #region 厂商私有常量
        /// <summary>GT_Stop平滑停止</summary>
        private const long GTS_STOP_SMOOTH = 0;
        /// <summary>GT_Stop紧急停止</summary>
        private const long GTS_STOP_EMERGENCY = 1;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        private static readonly HashSet<short> _openedChannels = new HashSet<short>();

        private CancellationTokenSource? _monitorCts;
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeDone = Array.Empty<bool>();
        private double[] _velOverride = Array.Empty<double>(); // <=0表示未用SetSpeed覆盖

        /// <summary>固高轴号（逻辑索引+1）</summary>
        private short AxisNo(int axis) => (short)(axis + 1);

        /// <summary>轴位掩码：1&lt;&lt;逻辑索引</summary>
        private long AxisMask(int axis) => 1L << axis;

        #region 连接/断开
        protected override EMotionResult ConnectRefactor()
        {
            try
            {
                lock (_sync)
                {
                    if (_isConnected)
                        return EMotionResult.Success;
                    if (AxisCount <= 0)
                        return EMotionResult.InvalidParam;

                    short channel = (short)Param.CardNo;
                    var ret = GTS.GT_Open(channel, 1);
                    if (ret != 0)
                        return EMotionResult.Fail;
                    _openedChannels.Add(channel);

                    // 下载配置文件（加载后需清状态生效）
                    if (!string.IsNullOrEmpty(Param.ConfigFile))
                        GTS.GT_LoadConfig(Param.ConfigFile);

                    for (int i = 0; i < AxisCount; i++)
                        GTS.GT_ClrSts(AxisNo(i), 1);

                    _homing = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _velOverride = new double[AxisCount];

                    StartMonitor();
                    return EMotionResult.Success;
                }
            }
            catch (DllNotFoundException)
            {
                return EMotionResult.Fail;   // 未安装固高驱动
            }
            catch (BadImageFormatException)
            {
                return EMotionResult.Fail;   // DLL位数与进程不一致
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

                _openedChannels.Remove((short)Param.CardNo);
                if (_openedChannels.Count == 0)
                    GTS.GT_Close();          // 最后一张卡断开时关闭库

                _homing = Array.Empty<bool>();
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 伺服控制
        protected override EMotionResult ServoOnRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                return GTS.GT_AxisOn(AxisNo(axis)) == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = GTS.GT_AxisOff(AxisNo(axis));
                if (ret == 0)
                {
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
                _homeDone[axis] = false;
                _homing[axis] = false;
                // 清除驱动器报警标志、跟随误差越限、限位触发标志；驱动器本体ALM需按其手册复位
                return GTS.GT_ClrSts(AxisNo(axis), 1) == 0 ? EMotionResult.Success : EMotionResult.Fail;
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
                StartTrapMove(axis, ClampPulse(pulse));
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                double cur = 0;
                if (GTS.GT_GetAxisPrfPos(AxisNo(axis), out cur, 1, IntPtr.Zero) != 0)
                    return EMotionResult.Fail;
                long target = (long)cur + pulse;
                if (!CheckSoftLimit(axis, target))
                    return EMotionResult.InvalidParam;
                StartTrapMove(axis, ClampPulse(target));
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                short no = AxisNo(axis);
                var cfg = AxisConfig(axis);
                var jog = new GTS.TJogPrm { acc = ToAcc(cfg.Accel, axis), dec = ToAcc(cfg.Decel, axis), smooth = 0 };
                GTS.GT_PrfJog(no);
                GTS.GT_SetJogPrm(no, ref jog);
                double vel = EffectiveVelocity(axis) * cfg.PulseEquivalent;
                GTS.GT_SetVel(no, direction > 0 ? vel : -vel);   // 负速度=负方向
                GTS.GT_Update(AxisMask(axis));
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                var option = stopMode == EStopMode.Emergency ? GTS_STOP_EMERGENCY : GTS_STOP_SMOOTH;
                return GTS.GT_Stop(AxisMask(axis), option) == 0 ? EMotionResult.Success : EMotionResult.Fail;
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
            long mask = AxisCount >= 64 ? -1L : (1L << AxisCount) - 1;
            var option = stopMode == EStopMode.Emergency ? GTS_STOP_EMERGENCY : GTS_STOP_SMOOTH;
            GTS.GT_Stop(mask, option);
            for (int i = 0; i < _homing.Length; i++)
                _homing[i] = false;
        }

        /// <summary>启动点位(梯形)运动</summary>
        private void StartTrapMove(int axis, int targetPulse)
        {
            short no = AxisNo(axis);
            var cfg = AxisConfig(axis);
            double vel = EffectiveVelocity(axis) * cfg.PulseEquivalent;
            var trap = new GTS.TTrapPrm
            {
                acc = ToAcc(cfg.Accel, axis),
                dec = ToAcc(cfg.Decel, axis),
                velStart = Math.Max(1, cfg.VelLow * cfg.PulseEquivalent),
                smoothTime = 0,
            };
            GTS.GT_PrfTrap(no);
            GTS.GT_SetTrapPrm(no, ref trap);
            GTS.GT_SetPos(no, targetPulse);
            GTS.GT_SetVel(no, vel);
            GTS.GT_Update(AxisMask(axis));
        }

        /// <summary>运动前置检查（连接、伺服使能）</summary>
        private bool CheckReady(int axis) => _isConnected && IsServoOn(axis);

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

        /// <summary>加速度换算：单位/s²×当量 → pulse/ms²（÷1e6）</summary>
        private double ToAcc(double unitPerS2, int axis)
            => unitPerS2 * AxisConfig(axis).PulseEquivalent / 1_000_000.0;

        private static int ClampPulse(long pulse) => (int)Math.Clamp(pulse, int.MinValue, int.MaxValue);

        /// <summary>伺服使能状态查询（状态字bit11，异常时回退到本地跟踪）</summary>
        private bool IsServoOn(int axis)
        {
            if (GTS.GT_GetSts(AxisNo(axis), out int sts, 1, IntPtr.Zero) != 0)
                return false;
            return (sts & GTS.GTS_STS_SERVO_ON) != 0;
        }
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
                        GTS.GT_ZeroPos(AxisNo(axis), 1);
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        short no = AxisNo(axis);
                        double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
                        var prm = new GTS.THomePrm
                        {
                            mode = ToGoogolHomeMode(cfg.HomeMode),
                            moveDir = (short)cfg.HomeDirection,
                            indexDir = (short)cfg.HomeDirection,
                            edge = 0,
                            triggerIndex = -1,
                            velHigh = Math.Max(1, cfg.HomeVelocity * equiv),
                            velLow = Math.Max(1, cfg.VelLow * equiv),
                            acc = ToAcc(cfg.Accel, axis),
                            dec = ToAcc(cfg.Decel, axis),
                            smoothTime = 0,
                            homeOffset = ClampPulse(ToPulse(axis, cfg.HomeOffset)),
                            searchHomeDistance = 0,
                            searchIndexDistance = 0,
                            escapeStep = 0,
                        };
                        GTS.GT_ClrSts(no, 1);
                        _homing[axis] = true;
                        _homeDone[axis] = false;
                        var ret = GTS.GT_GoHome(no, ref prm);
                        if (ret != 0)
                            _homing[axis] = false;
                        return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
                }
            }
        }

        /// <summary>通用回零模式映射到固高Smart Home模式值</summary>
        private static short ToGoogolHomeMode(EHomeMode mode)
        {
            return mode switch
            {
                EHomeMode.ZPhase => GTS.HOME_MODE_HOME_INDEX,
                EHomeMode.LimitSwitch => GTS.HOME_MODE_LIMIT,
                _ => GTS.HOME_MODE_HOME,
            };
        }

        /// <summary>回零完成检测：stage==END且无错误后置Homed并施加偏移</summary>
        private void CheckHomeDone(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected || !_homing[axis])
                    return;
                var st = new GTS.THomeStatus();
                if (GTS.GT_GetHomeStatus(AxisNo(axis), ref st) != 0)
                    return;
                if (st.stage < GTS.HOME_STAGE_END)
                    return;
                _homing[axis] = false;
                if (st.error != GTS.HOME_ERROR_NONE)
                    return;
                _homeDone[axis] = true;

                double offset = AxisConfig(axis).HomeOffset;
                if (Math.Abs(offset) > double.Epsilon && GTS.GT_GetAxisPrfPos(AxisNo(axis), out double cur, 1, IntPtr.Zero) == 0)
                    GTS.GT_SetPrfPos(AxisNo(axis), ClampPulse(ToPulse(axis, offset)) + (int)cur);
            }
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
                var ret = GTS.GT_GetAxisPrfPos(AxisNo(axis), out double pos, 1, IntPtr.Zero);
                pulse = (long)pos;
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = GTS.GT_GetAxisEncPos(AxisNo(axis), out double pos, 1, IntPtr.Zero);
                pulse = (long)pos;
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (GTS.GT_GetSts(AxisNo(axis), out int sts, 1, IntPtr.Zero) != 0)
                    return EMotionResult.Fail;
                // 打包：低32位=固高状态字；bit32=运动中（规划速度非零）
                bool moving = (sts & GTS.GTS_STS_ZERO_VEL) == 0;
                raw = (uint)sts | ((moving ? 1L : 0L) << 32);
                return EMotionResult.Success;
            }
        }

        /// <summary>将固高状态字映射为通用轴状态标志</summary>
        protected override EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            int sts = unchecked((int)raw);
            var flags = EAxisStatusFlag.None;
            if ((sts & GTS.GTS_STS_ALARM) != 0)
                flags |= EAxisStatusFlag.Alarm;
            if ((sts & GTS.GTS_STS_POS_LIMIT) != 0)
                flags |= EAxisStatusFlag.PositiveLimit;
            if ((sts & GTS.GTS_STS_NEG_LIMIT) != 0)
                flags |= EAxisStatusFlag.NegativeLimit;
            if ((raw & (1L << 32)) != 0)
                flags |= EAxisStatusFlag.Moving;
            else
                flags |= EAxisStatusFlag.InPosition;
            if ((sts & GTS.GTS_STS_SERVO_ON) != 0)
                flags |= EAxisStatusFlag.Enabled;
            lock (_sync)
            {
                if (_homeDone[axis])
                    flags |= EAxisStatusFlag.Homed;
            }
            return flags;
        }
        #endregion

        #region IO控制
        protected override EMotionResult GetInputRefactor(int index, out bool state)
        {
            state = false;
            lock (_sync)
            {
                if (!_isConnected || index < 0 || index > 31)
                    return !_isConnected ? EMotionResult.NotConnected : EMotionResult.InvalidParam;
                long value = 0;
                if (GTS.GT_GetDi(GTS.MC_GPI, ref value) != 0)
                    return EMotionResult.Fail;
                state = (value & (1L << index)) != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            state = false;
            lock (_sync)
            {
                if (!_isConnected || index < 0 || index > 31)
                    return !_isConnected ? EMotionResult.NotConnected : EMotionResult.InvalidParam;
                long value = 0;
                if (GTS.GT_GetDo(GTS.MC_GPO, ref value) != 0)
                    return EMotionResult.Fail;
                state = (value & (1L << index)) != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (index < 0 || index > short.MaxValue)
                    return EMotionResult.InvalidParam;
                var ret = GTS.GT_SetDoBit(GTS.MC_GPO, (short)index, (short)(state ? 1 : 0));
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }
        #endregion

        #region 后台监控（报警事件 + 回零完成检测）
        private void StartMonitor()
        {
            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;
            _ = Task.Run(() =>
            {
                var lastAlm = new bool[_homing.Length];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < _homing.Length; i++)
                        {
                            if (GTS.GT_GetSts(AxisNo(i), out int sts, 1, IntPtr.Zero) != 0)
                                continue;
                            bool alm = (sts & GTS.GTS_STS_ALARM) != 0;
                            if (alm && !lastAlm[i])
                                RaiseAlarm(i, $"固高卡报警：{Param.Name}(轴{i})", sts);
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
        #endregion
    }
}
