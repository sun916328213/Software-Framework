using MotionControl.Definition;

namespace MotionControl.Vendors.ADLINK
{
    /// <summary>
    /// 凌华ADLINK轴卡控制类（基于APS函数库，支持PCI-8154/8144/8254、AMP、EtherCAT等系列）
    /// <para>
    /// 使用前提：
    /// 1. 目标机已安装APS SDK驱动（APS168x64.dll/APS168.dll位于系统目录）；
    /// 2. 建议先用MotionCreatorPro2完成硬件配置并导出XML，填入MotionCardParam.ConfigFile；
    /// 3. 轴索引即Param.Axises列表顺序，映射为该板卡的全局轴ID（从起始轴ID起依次排列）；
    /// 4. 所有位置/速度以脉冲为单位与APS交互，用户单位由基类按脉冲当量自动换算。
    /// </para>
    /// </summary>
    internal class AdlinkHelper : MotionControlBase
    {
        #region 厂商私有常量（如与所用卡型手册不符请在此调整）
        /// <summary>回零模式：仅ORG信号</summary>
        private const int ADL_HOME_ORG = 0;
        /// <summary>回零模式：ORG+EZ</summary>
        private const int ADL_HOME_ORG_EZ = 1;
        /// <summary>回零模式：EL限位作原点（部分卡型支持，值以手册为准）</summary>
        private const int ADL_HOME_EL = 3;
        /// <summary>回零方向：正向</summary>
        private const int ADL_HOME_DIR_POSITIVE = 0;
        /// <summary>回零方向：负向</summary>
        private const int ADL_HOME_DIR_NEGATIVE = 1;
        /// <summary>DIO每组位数（32点/组）</summary>
        private const int DIO_GROUP_BITS = 32;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        /// <summary>全局已初始化板卡位掩码（APS_initial为进程级全局操作，跨实例共享）</summary>
        private static int _openBoardMask;

        private CancellationTokenSource? _monitorCts;
        private int[] _axisIds = Array.Empty<int>();      // 各逻辑轴对应的全局Axis_ID
        private bool[] _servoOn = Array.Empty<bool>();
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeSeen = Array.Empty<bool>();   // 回零中已观察到HMV位（防误判）
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

                    var param = Param;
                    int boardMask = 1 << param.CardNo;

                    // 加载MotionCreatorPro2导出的配置文件（须在initial之前）
                    if (!string.IsNullOrEmpty(param.ConfigFile))
                    {
                        var retCfg = APS168.APS_load_param_from_file(param.ConfigFile);
                        if (retCfg != APS168.ERR_NoError)
                            return EMotionResult.Fail;
                    }

                    if ((_openBoardMask & boardMask) == 0)
                    {
                        var retInit = APS168.APS_initial(ref boardMask, 0);
                        if (retInit != APS168.ERR_NoError && retInit != APS168.ERR_DeviceAlreadyInitialed)
                            return EMotionResult.Fail;
                        _openBoardMask |= 1 << param.CardNo;
                    }

                    // 获取板卡全局起始轴ID与轴总数，建立逻辑轴映射
                    int startAxisId = 0, totalAxes = 0;
                    var retAxis = APS168.APS_get_first_axisId(param.CardNo, ref startAxisId, ref totalAxes);
                    if (retAxis != APS168.ERR_NoError)
                        return EMotionResult.Fail;
                    if (AxisCount <= 0 || AxisCount > totalAxes)
                        return EMotionResult.InvalidParam;

                    _axisIds = new int[AxisCount];
                    _servoOn = new bool[AxisCount];
                    _homing = new bool[AxisCount];
                    _homeSeen = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _velOverride = new double[AxisCount];

                    for (int i = 0; i < AxisCount; i++)
                    {
                        _axisIds[i] = startAxisId + i;
                        ApplyAxisProfile(i);
                    }

                    StartMonitor();

                    return EMotionResult.Success;
                }
            }
            catch (DllNotFoundException)
            {
                // 系统中未安装APS驱动
                return EMotionResult.Fail;
            }
            catch (BadImageFormatException)
            {
                // DLL位数与进程位数不一致
                return EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 将轴参数（速度/加减速/回零参数）下发到板卡
        /// </summary>
        private void ApplyAxisProfile(int axis)
        {
            var cfg = AxisConfig(axis);
            int id = _axisIds[axis];
            int acc = ToAccDec(cfg.Accel, axis);
            int dec = ToAccDec(cfg.Decel, axis);

            APS168.APS_set_axis_param(id, APS168.PRA_MAX_VELOCITY, ToSpeed(cfg.VelHigh, axis));
            APS168.APS_set_axis_param(id, APS168.PRA_ACC, acc);
            APS168.APS_set_axis_param(id, APS168.PRA_DEC, dec);

            APS168.APS_set_axis_param(id, APS168.PRA_HOME_MODE, ToAdlinkHomeMode(cfg.HomeMode));
            APS168.APS_set_axis_param(id, APS168.PRA_HOME_DIR,
                cfg.HomeDirection == EDirection.Positive ? ADL_HOME_DIR_POSITIVE : ADL_HOME_DIR_NEGATIVE);
            APS168.APS_set_axis_param(id, APS168.PRA_HOME_ACC, acc);
            APS168.APS_set_axis_param(id, APS168.PRA_HOME_VS, ToSpeed(cfg.VelLow, axis));
            APS168.APS_set_axis_param(id, APS168.PRA_HOME_VM, ToSpeed(cfg.HomeVelocity, axis));
        }

        /// <summary>通用回零模式映射到凌华回零模式值</summary>
        private static int ToAdlinkHomeMode(EHomeMode mode)
        {
            return mode switch
            {
                EHomeMode.ZPhase => ADL_HOME_ORG_EZ,
                EHomeMode.LimitSwitch => ADL_HOME_EL,
                _ => ADL_HOME_ORG,
            };
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

                _openBoardMask &= ~(1 << Param.CardNo);
                if (_openBoardMask == 0)
                {
                    // 本进程最后一张卡断开时才关闭整个APS库（APS_close关闭所有板卡）
                    APS168.APS_close();
                }

                _axisIds = Array.Empty<int>();
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
                var ret = APS168.APS_set_servo_on(_axisIds[axis], 1);
                if (ret == APS168.ERR_NoError)
                    _servoOn[axis] = true;
                return ToResult(ret);
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = APS168.APS_set_servo_on(_axisIds[axis], 0);
                if (ret == APS168.ERR_NoError)
                {
                    _servoOn[axis] = false;
                    _homing[axis] = false;
                    _homeDone[axis] = false;
                }
                return ToResult(ret);
            }
        }

        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;

                // 脉冲型板卡无统一软件清报警指令：多数驱动器通过伺服重新上电复位ALM，
                // 此处执行伺服关-延时-开循环；若驱动器需专用RST信号清障请改为输出RST。
                bool wasOn = _servoOn[axis];
                APS168.APS_set_servo_on(_axisIds[axis], 0);
                Thread.Sleep(100);
                if (wasOn)
                    APS168.APS_set_servo_on(_axisIds[axis], 1);

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
                _velOverride[axis] = velPulse;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult MoveAbsRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return EMotionResult.NotConnected;
                if (!CheckSoftLimit(axis, pulse))
                    return EMotionResult.InvalidParam;
                var ret = APS168.APS_absolute_move(_axisIds[axis], ClampPulse(pulse), EffectiveSpeed(axis));
                return ToResult(ret);
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return EMotionResult.NotConnected;
                int cur = 0;
                if (APS168.APS_get_command(_axisIds[axis], ref cur) != APS168.ERR_NoError)
                    return EMotionResult.Fail;
                long target = cur + pulse;
                if (!CheckSoftLimit(axis, target))
                    return EMotionResult.InvalidParam;
                var ret = APS168.APS_relative_move(_axisIds[axis], ClampPulse(pulse), EffectiveSpeed(axis));
                return ToResult(ret);
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return EMotionResult.NotConnected;
                var ret = APS168.APS_velocity_move(_axisIds[axis], direction > 0 ? EffectiveSpeed(axis) : -EffectiveSpeed(axis));
                return ToResult(ret);
            }
        }

        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                var ret = stopMode == EStopMode.Emergency
                    ? APS168.APS_emg_stop(_axisIds[axis])
                    : APS168.APS_stop_move(_axisIds[axis]);
                return ToResult(ret);
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
            for (int i = 0; i < _axisIds.Length; i++)
            {
                _homing[i] = false;
                if (stopMode == EStopMode.Emergency)
                    APS168.APS_emg_stop(_axisIds[i]);
                else
                    APS168.APS_stop_move(_axisIds[i]);
            }
        }

        /// <summary>运动前置检查（连接、伺服使能、无报警）</summary>
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

        /// <summary>当前生效速度（pulse/s）：SetSpeed覆盖优先，否则取配置VelHigh</summary>
        private int EffectiveSpeed(int axis)
        {
            var cfg = AxisConfig(axis);
            double velUnit = _velOverride[axis] > 0 ? _velOverride[axis] : cfg.VelHigh;
            return ClampPulse((long)Math.Round(velUnit * cfg.PulseEquivalent));
        }

        /// <summary>加速度换算：单位/s² × 当量 → pulse/ss</summary>
        private int ToAccDec(double unitPerS2, int axis)
            => ClampPulse((long)Math.Round(unitPerS2 * AxisConfig(axis).PulseEquivalent));

        /// <summary>速度换算：单位/s × 当量 → pulse/s</summary>
        private int ToSpeed(double unitPerS, int axis)
            => ClampPulse((long)Math.Round(unitPerS * AxisConfig(axis).PulseEquivalent));

        private static int ClampPulse(long pulse)
            => (int)Math.Clamp(pulse, int.MinValue, int.MaxValue);

        private static EMotionResult ToResult(int apsRet)
            => apsRet == APS168.ERR_NoError ? EMotionResult.Success : EMotionResult.Fail;
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
                        APS168.APS_set_command(_axisIds[axis], 0);
                        APS168.APS_set_position(_axisIds[axis], 0);
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        // 回零曲线参数已在Connect时写入板卡，此处启动即可；
                        // 完成检测由监控线程负责（HMV位消失后置Homed并施加偏移）
                        _homing[axis] = true;
                        _homeSeen[axis] = false;
                        _homeDone[axis] = false;
                        var ret = APS168.APS_home_move(_axisIds[axis]);
                        if (ret != APS168.ERR_NoError)
                            _homing[axis] = false;
                        return ToResult(ret);
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
                int pos = 0;
                var ret = APS168.APS_get_command(_axisIds[axis], ref pos);
                pulse = pos;
                return ToResult(ret);
            }
        }

        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                int pos = 0;
                var ret = APS168.APS_get_position(_axisIds[axis], ref pos);
                pulse = pos;
                return ToResult(ret);
            }
        }

        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                int mts = APS168.APS_motion_status(_axisIds[axis]);
                int mio = APS168.APS_motion_io_status(_axisIds[axis]);
                // 打包：低32位=运动状态字，高32位=IO状态字，由ParseAxisFlags解析
                raw = (uint)mts | ((long)mio << 32);
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 将凌华状态字映射为通用轴状态标志
        /// </summary>
        protected override EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            int mts = unchecked((int)raw);
            int mio = unchecked((int)(raw >> 32));

            var flags = EAxisStatusFlag.None;
            if ((mts & APS168.MTS_MOVING_MASK) != 0)
                flags |= EAxisStatusFlag.Moving;
            if ((mio & APS168.MIO_INP) != 0)
                flags |= EAxisStatusFlag.InPosition;
            if ((mio & APS168.MIO_ALM) != 0)
                flags |= EAxisStatusFlag.Alarm;
            if ((mio & (APS168.MIO_PEL | APS168.MIO_SPEL)) != 0)
                flags |= EAxisStatusFlag.PositiveLimit;
            if ((mio & (APS168.MIO_MEL | APS168.MIO_SMEL)) != 0)
                flags |= EAxisStatusFlag.NegativeLimit;
            if ((mio & APS168.MIO_EMG) != 0)
                flags |= EAxisStatusFlag.EmergencyStop;
            if ((mts & APS168.MTS_HMV) != 0)
                flags |= EAxisStatusFlag.Homed;

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
            if (index < 0 || index >= DIO_GROUP_BITS * 16)
            {
                state = false;
                return EMotionResult.InvalidParam;
            }
            lock (_sync)
            {
                if (!_isConnected)
                {
                    state = false;
                    return EMotionResult.NotConnected;
                }
                int group = index / DIO_GROUP_BITS;
                int bit = index % DIO_GROUP_BITS;
                int data = 0;
                var ret = APS168.APS_read_d_input(Param.CardNo, group, ref data);
                if (ret != APS168.ERR_NoError)
                {
                    state = false;
                    return EMotionResult.Fail;
                }
                state = (data & (1 << bit)) != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            if (index < 0 || index >= DIO_GROUP_BITS * 16)
            {
                state = false;
                return EMotionResult.InvalidParam;
            }
            lock (_sync)
            {
                if (!_isConnected)
                {
                    state = false;
                    return EMotionResult.NotConnected;
                }
                int group = index / DIO_GROUP_BITS;
                int bit = index % DIO_GROUP_BITS;
                int data = 0;
                var ret = APS168.APS_read_d_output(Param.CardNo, group, ref data);
                if (ret != APS168.ERR_NoError)
                {
                    state = false;
                    return EMotionResult.Fail;
                }
                state = (data & (1 << bit)) != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (index < 0)
                    return EMotionResult.InvalidParam;

                int group = index / DIO_GROUP_BITS;
                int bit = index % DIO_GROUP_BITS;
                int data = 0;
                if (APS168.APS_read_d_output(Param.CardNo, group, ref data) != APS168.ERR_NoError)
                    return EMotionResult.Fail;
                data = state ? data | (1 << bit) : data & ~(1 << bit);
                return ToResult(APS168.APS_write_d_output(Param.CardNo, group, data));
            }
        }
        #endregion

        #region 后台监控（报警事件 + 回零完成检测）
        /// <summary>
        /// 启动后台监控线程：轮询各轴IO状态字，报警上升沿抛出AlarmOccurred；检测回零完成后置Homed并施加偏移
        /// </summary>
        private void StartMonitor()
        {
            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;
            _ = Task.Run(() =>
            {
                var lastAlm = new bool[_axisIds.Length];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < _axisIds.Length; i++)
                        {
                            int mio = APS168.APS_motion_io_status(_axisIds[i]);
                            bool alm = (mio & APS168.MIO_ALM) != 0;
                            if (alm && !lastAlm[i])
                                RaiseAlarm(i, $"凌华卡报警：{Param.Name}(轴{i})", mio);
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

        /// <summary>回零完成检测：HMV位出现后消失且轴停止，判定回零完成</summary>
        private void CheckHomeDone(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected || !_homing[axis])
                    return;
                int mts = APS168.APS_motion_status(_axisIds[axis]);
                if ((mts & APS168.MTS_HMV) != 0)
                {
                    _homeSeen[axis] = true;
                    return;
                }
                if (!_homeSeen[axis] || (mts & APS168.MTS_MOVING_MASK) != 0)
                    return;

                _homing[axis] = false;
                _homeDone[axis] = true;

                // 施加回零偏移：指令/反馈位置同时偏移
                double offset = AxisConfig(axis).HomeOffset;
                if (Math.Abs(offset) > double.Epsilon)
                {
                    int off = ClampPulse(ToPulse(axis, offset));
                    APS168.APS_set_command(_axisIds[axis], off);
                    APS168.APS_set_position(_axisIds[axis], off);
                }
            }
        }
        #endregion
    }
}
