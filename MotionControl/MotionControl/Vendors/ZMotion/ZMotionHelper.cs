using MotionControl.Definition;

namespace MotionControl.Vendors.ZMotion
{
    /// <summary>
    /// 正运动ZMotion控制器/控制卡控制类（基于zauxdll.dll，需同目录zmotion.dll）
    /// <para>
    /// 使用前提：
    /// 1. 目标机已安装正运动PC函数库（zauxdll.dll/zmotion.dll位于系统目录或程序目录）；
    /// 2. 通过MotionCardParam.IpAddress/TcpPort指定控制器地址（默认502）；
    /// 3. 连接时将各轴UNITS设为1（脉冲=用户当量），全部位置速度以脉冲交互；
    /// 4. 轴索引即Param.Axises列表顺序（0基）。
    /// </para>
    /// </summary>
    internal class ZMotionHelper : MotionControlBase
    {
        #region 厂商私有常量
        /// <summary>回零完成判定前的最短等待（毫秒），防止DATUM启动瞬间误判</summary>
        private const int HomeSettleMs = 500;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        private IntPtr _handle = IntPtr.Zero;
        private CancellationTokenSource? _monitorCts;
        private DateTime[] _homeStartAt = Array.Empty<DateTime>();
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
                    if (AxisCount <= 0)
                        return EMotionResult.InvalidParam;
                    if (string.IsNullOrWhiteSpace(Param.IpAddress))
                        return EMotionResult.InvalidParam;

                    var ret = ZAux.ZAux_OpenEth(Param.IpAddress, (uint)Param.TcpPort, out _handle);
                    if (ret != 0 || _handle == IntPtr.Zero)
                    {
                        _handle = IntPtr.Zero;
                        return EMotionResult.Fail;
                    }

                    // UNITS=1：控制器原生以脉冲为单位，与基类换算约定一致
                    for (int i = 0; i < AxisCount; i++)
                        ZAux.ZAux_Direct_SetUnits(_handle, i, 1);

                    _homing = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _homeStartAt = new DateTime[AxisCount];
                    _velOverride = new double[AxisCount];

                    StartMonitor();
                    return EMotionResult.Success;
                }
            }
            catch (DllNotFoundException)
            {
                return EMotionResult.Fail;   // 未安装正运动PC函数库
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
                if (_handle != IntPtr.Zero)
                {
                    ZAux.ZAux_Close(_handle);
                    _handle = IntPtr.Zero;
                }
                _homing = Array.Empty<bool>();
                return EMotionResult.Success;
            }
        }
        #endregion

        #region 伺服控制
        protected override EMotionResult ServoOnRefactor(int axis)
        {
            // 正运动轴使能由控制器参数AXIS_ENABLE/WDOG管理（脉冲轴默认无需软件使能指令）
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                _homeDone[axis] = false;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                // DATUM(0)清除控制器轴状态错误；驱动器本体报警按其手册复位
                _homeDone[axis] = false;
                _homing[axis] = false;
                var ret = ZAux.ZAux_Direct_Single_Datum(_handle, axis, ZAux.ZMC_DATUM_CLEAR_STATUS);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
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
                _velOverride[axis] = velPulse;
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
                ApplyMotionParams(axis);
                var ret = ZAux.ZAux_Direct_Single_Amove(_handle, axis, pulse);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!CheckSoftLimit(axis, CurrentPulse(axis) + pulse))
                    return EMotionResult.InvalidParam;
                ApplyMotionParams(axis);
                var ret = ZAux.ZAux_Direct_Single_Move(_handle, axis, pulse);
                return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                ApplyMotionParams(axis);
                var ret = ZAux.ZAux_Direct_Single_Vmove(_handle, axis, direction > 0 ? 1 : -1);
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
                int mode = stopMode == EStopMode.Emergency ? ZAux.ZMC_CANCEL_IMMEDIATE : ZAux.ZMC_CANCEL_ALL;
                var ret = ZAux.ZAux_Direct_Single_Cancel(_handle, axis, mode);
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
            for (int i = 0; i < _homing.Length; i++)
            {
                _homing[i] = false;
                int mode = stopMode == EStopMode.Emergency ? ZAux.ZMC_CANCEL_IMMEDIATE : ZAux.ZMC_CANCEL_ALL;
                ZAux.ZAux_Direct_Single_Cancel(_handle, i, mode);
            }
        }

        /// <summary>下发当前速度与加减速</summary>
        private void ApplyMotionParams(int axis)
        {
            var cfg = AxisConfig(axis);
            float vel = (float)EffectiveVelocity(axis);
            ZAux.ZAux_Direct_SetSpeed(_handle, axis, vel);
            ZAux.ZAux_Direct_SetAccel(_handle, axis, (float)(cfg.Accel * cfg.PulseEquivalent));
            ZAux.ZAux_Direct_SetDecel(_handle, axis, (float)(cfg.Decel * cfg.PulseEquivalent));
        }

        /// <summary>运动前置检查（连接、伺服就绪）</summary>
        private bool CheckReady(int axis) => _isConnected && IsServoReady(axis);

        /// <summary>软限位检查</summary>
        private bool CheckSoftLimit(int axis, long pulse)
        {
            var cfg = AxisConfig(axis);
            if (!cfg.EnableSoftLimit)
                return true;
            return pulse <= ToPulse(axis, cfg.SoftLimitPositive)
                && pulse >= ToPulse(axis, cfg.SoftLimitNegative);
        }

        /// <summary>当前生效速度（pulse/s）：SetSpeed覆盖优先，否则取配置VelHigh×当量</summary>
        private double EffectiveVelocity(int axis)
        {
            var cfg = AxisConfig(axis);
            double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
            return _velOverride[axis] > 0 ? _velOverride[axis] : cfg.VelHigh * equiv;
        }

        /// <summary>当前指令位置（pulse）</summary>
        private long CurrentPulse(int axis)
        {
            float pos = 0;
            ZAux.ZAux_Direct_GetDpos(_handle, axis, ref pos);
            return (long)pos;
        }

        /// <summary>正运动的使能由控制器侧管理，此处以IDLE状态可读作为就绪判据</summary>
        private bool IsServoReady(int axis)
        {
            int idle = 0;
            return ZAux.ZAux_Direct_GetIfIdle(_handle, axis, ref idle) == 0;
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
                        ZAux.ZAux_Direct_SetDpos(_handle, axis, 0);
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        ApplyMotionParams(axis);
                        // 回零速度：DATUM使用SPEED/CREEP参数，这里将SPEED设为回零速度
                        ZAux.ZAux_Direct_SetSpeed(_handle, axis,
                            (float)Math.Max(1, cfg.HomeVelocity * Math.Max(cfg.PulseEquivalent, 1)));
                        _homing[axis] = true;
                        _homeDone[axis] = false;
                        _homeStartAt[axis] = DateTime.Now;
                        var ret = ZAux.ZAux_Direct_Single_Datum(_handle, axis, ToZmotionHomeMode(cfg.HomeMode));
                        if (ret != 0)
                            _homing[axis] = false;
                        return ret == 0 ? EMotionResult.Success : EMotionResult.Fail;
                }
            }
        }

        /// <summary>通用回零模式映射到正运动DATUM模式值</summary>
        private static int ToZmotionHomeMode(EHomeMode mode)
        {
            return mode switch
            {
                EHomeMode.ZPhase => ZAux.ZMC_DATUM_ORIGIN_EZ,
                EHomeMode.LimitSwitch => ZAux.ZMC_DATUM_LIMIT_HOME,
                _ => ZAux.ZMC_DATUM_ORIGIN_STOP,
            };
        }

        /// <summary>回零完成检测：启动超过HomeSettleMs后轴停止即视为完成，并施加偏移</summary>
        private void CheckHomeDone(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected || !_homing[axis])
                    return;
                if ((DateTime.Now - _homeStartAt[axis]).TotalMilliseconds < HomeSettleMs)
                    return;
                int idle = 0;
                if (ZAux.ZAux_Direct_GetIfIdle(_handle, axis, ref idle) != 0 || idle != 0)
                    return;   // -1表示仍在运动

                _homing[axis] = false;
                _homeDone[axis] = true;

                double offset = AxisConfig(axis).HomeOffset;
                if (Math.Abs(offset) > double.Epsilon)
                {
                    double offPulse = offset * Math.Max(AxisConfig(axis).PulseEquivalent, 1);
                    ZAux.ZAux_Direct_SetDpos(_handle, axis, (float)offPulse);
                }
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
                float pos = 0;
                var ret = ZAux.ZAux_Direct_GetDpos(_handle, axis, ref pos);
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
                float pos = 0;
                var ret = ZAux.ZAux_Direct_GetMpos(_handle, axis, ref pos);
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
                int status = 0;
                if (ZAux.ZAux_Direct_GetAxisStatus(_handle, axis, ref status) != 0)
                    return EMotionResult.Fail;
                int idle = 0;
                bool moving = ZAux.ZAux_Direct_GetIfIdle(_handle, axis, ref idle) == 0 && idle != 0;
                // 打包：低32位=AXISSTATUS；bit32=运动中
                raw = (uint)status | ((moving ? 1L : 0L) << 32);
                return EMotionResult.Success;
            }
        }

        /// <summary>将正运动状态字映射为通用轴状态标志</summary>
        protected override EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            int status = unchecked((int)raw);
            var flags = EAxisStatusFlag.None;
            if ((status & ZAux.ZMC_STS_FOLLOW_ERROR) != 0)
                flags |= EAxisStatusFlag.Alarm;
            if ((status & ZAux.ZMC_STS_POS_LIMIT) != 0)
                flags |= EAxisStatusFlag.PositiveLimit;
            if ((status & ZAux.ZMC_STS_NEG_LIMIT) != 0)
                flags |= EAxisStatusFlag.NegativeLimit;
            if ((status & ZAux.ZMC_STS_EMERGENCY) != 0)
                flags |= EAxisStatusFlag.EmergencyStop;
            if ((raw & (1L << 32)) != 0)
                flags |= EAxisStatusFlag.Moving;
            else
                flags |= EAxisStatusFlag.InPosition;

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
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                uint v = 0;
                var ret = ZAux.ZAux_Direct_GetIn(_handle, index, ref v);
                if (ret != 0)
                    return EMotionResult.Fail;
                state = v != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            state = false;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                uint v = 0;
                var ret = ZAux.ZAux_Direct_GetOp(_handle, index, ref v);
                if (ret != 0)
                    return EMotionResult.Fail;
                state = v != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = ZAux.ZAux_Direct_SetOp(_handle, index, state ? 1u : 0u);
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
                            int status = 0;
                            if (ZAux.ZAux_Direct_GetAxisStatus(_handle, i, ref status) != 0)
                                continue;
                            bool alm = (status & (ZAux.ZMC_STS_FOLLOW_ERROR | ZAux.ZMC_STS_EMERGENCY)) != 0;
                            if (alm && !lastAlm[i])
                                RaiseAlarm(i, $"正运动控制器报警：{Param.Name}(轴{i})", status);
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
