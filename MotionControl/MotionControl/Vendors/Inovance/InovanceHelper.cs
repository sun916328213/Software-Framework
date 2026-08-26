using MotionControl.Definition;

namespace MotionControl.Vendors.Inovance
{
    /// <summary>
    /// 汇川控制器控制类（Modbus TCP寄存器映射方式）
    /// <para>
    /// 适配汇川H3U/H5U/Easy/AM系列PLC（网口Modbus TCP）及IMC控制卡。
    /// 上位机通过读写约定的寄存器块驱动PLC侧运动接口FB，寄存器布局由InovanceParam配置。
    /// </para>
    /// <para>
    /// 约定的每轴寄存器块布局（PLC侧程序须按此实现或调整InovanceParam）：
    /// - 控制字(WORD)：bit0使能 bit1相对启动 bit2绝对启动 bit3点动正 bit4点动负 bit5回零启动 bit6停止 bit7清错
    /// - 状态字(WORD)：bit0就绪 bit1运动中 bit2报警 bit3正限位 bit4负限位 bit5回零完成
    /// - 目标位置(DINT)、目标速度(REAL，用户单位/s)、当前位置(DINT)
    /// </para>
    /// </summary>
    internal class InovanceHelper : MotionControlBase
    {
        #region 控制字/状态字位定义
        private const ushort CTL_SERVO_ON = 1 << 0;
        private const ushort CTL_MOVE_REL = 1 << 1;
        private const ushort CTL_MOVE_ABS = 1 << 2;
        private const ushort CTL_JOG_POSITIVE = 1 << 3;
        private const ushort CTL_JOG_NEGATIVE = 1 << 4;
        private const ushort CTL_HOME_START = 1 << 5;
        private const ushort CTL_STOP = 1 << 6;
        private const ushort CTL_CLEAR_ALARM = 1 << 7;

        private const ushort STS_READY = 1 << 0;
        private const ushort STS_MOVING = 1 << 1;
        private const ushort STS_ALARM = 1 << 2;
        private const ushort STS_POS_LIMIT = 1 << 3;
        private const ushort STS_NEG_LIMIT = 1 << 4;
        private const ushort STS_HOMED = 1 << 5;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        private readonly ModbusTcpClient _client = new ModbusTcpClient();
        private CancellationTokenSource? _monitorCts;
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeDone = Array.Empty<bool>();

        /// <summary>强类型参数（含寄存器映射）</summary>
        private InovanceParam InvParam => Param as InovanceParam ?? new InovanceParam();

        #region 连接/断开
        protected override EMotionResult ConnectRefactor()
        {
            lock (_sync)
            {
                if (_isConnected)
                    return EMotionResult.Success;
                if (AxisCount <= 0)
                    return EMotionResult.InvalidParam;
                if (string.IsNullOrWhiteSpace(Param.IpAddress))
                    return EMotionResult.InvalidParam;

                if (!_client.Connect(Param.IpAddress, Param.TcpPort))
                    return EMotionResult.Fail;

                // 连接探测：读0号轴状态字
                if (_client.ReadHoldingRegisters(InvParam.SlaveId, InvParam.StatusWordAddress(0), 1) == null)
                {
                    _client.Disconnect();
                    return EMotionResult.Fail;
                }

                _homing = new bool[AxisCount];
                _homeDone = new bool[AxisCount];
                StartMonitor();
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult DisConnectRefactor()
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.Success;
                StopAllInternal();
                _monitorCts?.Cancel();
                _monitorCts = null;
                _client.Dispose();
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
                return SetControlBits(axis, CTL_SERVO_ON, CTL_SERVO_ON);
            }
        }

        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var ret = SetControlBits(axis, 0, CTL_SERVO_ON);
                if (ret == EMotionResult.Success)
                {
                    _homing[axis] = false;
                    _homeDone[axis] = false;
                }
                return ret;
            }
        }

        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                // 置位清错位并保持一个轮询周期后清除
                var ret = SetControlBits(axis, CTL_CLEAR_ALARM, CTL_CLEAR_ALARM);
                Thread.Sleep(MonitorIntervalMs);
                SetControlBits(axis, 0, CTL_CLEAR_ALARM);
                _homeDone[axis] = false;
                _homing[axis] = false;
                return ret;
            }
        }
        #endregion

        #region 运动控制
        protected override EMotionResult SetSpeedRefactor(int axis, long velPulse)
        {
            // 速度在每次运动前按配置下发，此处无需缓存（用户单位直接写REAL）
            lock (_sync)
            {
                return _isConnected ? EMotionResult.Success : EMotionResult.NotConnected;
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
                WriteTarget(axis, pulse, EffectiveVelocity(axis));
                return SetControlBits(axis, CTL_MOVE_ABS, CTL_MOVE_ABS | CTL_JOG_POSITIVE | CTL_JOG_NEGATIVE | CTL_HOME_START);
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                long cur = ReadCurrentPosition(axis);
                if (!CheckSoftLimit(axis, cur + pulse))
                    return EMotionResult.InvalidParam;
                WriteTarget(axis, pulse, EffectiveVelocity(axis));
                return SetControlBits(axis, CTL_MOVE_REL, CTL_MOVE_REL | CTL_JOG_POSITIVE | CTL_JOG_NEGATIVE | CTL_HOME_START);
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                WriteTarget(axis, 0, EffectiveVelocity(axis));
                var bit = direction > 0 ? CTL_JOG_POSITIVE : CTL_JOG_NEGATIVE;
                return SetControlBits(axis, bit, CTL_MOVE_ABS | CTL_MOVE_REL | CTL_JOG_POSITIVE | CTL_JOG_NEGATIVE | CTL_HOME_START);
            }
        }

        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                return SetControlBits(axis, CTL_STOP, CTL_STOP);
            }
        }

        protected override EMotionResult StopAllRefactor(EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                StopAllInternal();
                return EMotionResult.Success;
            }
        }

        private void StopAllInternal()
        {
            for (int i = 0; i < AxisCount; i++)
            {
                _homing[i] = false;
                SetControlBits(i, CTL_STOP, CTL_STOP);
            }
        }

        /// <summary>运动前置检查（连接、就绪）</summary>
        private bool CheckReady(int axis)
        {
            if (!_isConnected)
                return false;
            var sts = ReadStatusWord(axis);
            return sts.HasValue && (sts.Value & STS_READY) != 0;
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
            return cfg.VelHigh;
        }

        /// <summary>读-改-写控制字：setBits置位，mask中的位先清零</summary>
        private EMotionResult SetControlBits(int axis, ushort setBits, ushort clearMask)
        {
            ushort addr = InvParam.ControlWordAddress(axis);
            var cur = _client.ReadHoldingRegisters(InvParam.SlaveId, addr, 1);
            if (cur == null)
                return EMotionResult.Fail;
            ushort word = (ushort)((cur[0] & ~clearMask) | setBits);
            return _client.WriteSingleRegister(InvParam.SlaveId, addr, (short)word)
                ? EMotionResult.Success : EMotionResult.Fail;
        }

        /// <summary>写目标位置与目标速度</summary>
        private void WriteTarget(int axis, long position, double velocity)
        {
            int pos = (int)Math.Clamp(position * 1.0, int.MinValue, int.MaxValue);
            // DINT低字在前；REAL按IEEE754两字（低字在前）
            short posLow = (short)(pos & 0xFFFF);
            short posHigh = (short)((pos >> 16) & 0xFFFF);
            var velBytes = BitConverter.GetBytes((float)velocity);
            short velLow = BitConverter.ToInt16(velBytes, 0);
            short velHigh = BitConverter.ToInt16(velBytes, 2);

            _client.WriteMultipleRegisters(InvParam.SlaveId, InvParam.TargetPositionAddress(axis), new[] { posLow, posHigh });
            _client.WriteMultipleRegisters(InvParam.SlaveId, InvParam.TargetVelocityAddress(axis), new[] { velLow, velHigh });
        }

        /// <summary>读取当前反馈位置（pulse）</summary>
        private long ReadCurrentPosition(int axis)
        {
            var data = _client.ReadHoldingRegisters(InvParam.SlaveId, InvParam.CurrentPositionAddress(axis), 2);
            if (data == null || data.Length < 2)
                return 0;
            return ((data[1] << 16) | (ushort)data[0]);
        }

        /// <summary>读取状态字，失败返回null</summary>
        private ushort? ReadStatusWord(int axis)
        {
            var data = _client.ReadHoldingRegisters(InvParam.SlaveId, InvParam.StatusWordAddress(axis), 1);
            return data == null ? null : (ushort)data[0];
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
                switch (AxisConfig(axis).HomeMode)
                {
                    case EHomeMode.None:
                        return EMotionResult.InvalidParam;
                    case EHomeMode.SetCurrentAsOrigin:
                        // 直接将当前位置寄存器清零
                        _client.WriteMultipleRegisters(InvParam.SlaveId, InvParam.CurrentPositionAddress(axis), new short[] { 0, 0 });
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        _homing[axis] = true;
                        _homeDone[axis] = false;
                        return SetControlBits(axis, CTL_HOME_START, CTL_HOME_START);
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
                pulse = ReadCurrentPosition(axis);
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            // 寄存器映射方式仅暴露一路位置
            return GetCommandPositionRefactor(axis, out pulse);
        }

        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var sts = ReadStatusWord(axis);
                if (sts == null)
                    return EMotionResult.Fail;
                raw = sts.Value;
                return EMotionResult.Success;
            }
        }

        /// <summary>将状态字映射为通用轴状态标志</summary>
        protected override EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            ushort sts = (ushort)raw;
            var flags = EAxisStatusFlag.None;
            if ((sts & STS_READY) != 0)
                flags |= EAxisStatusFlag.Enabled;
            if ((sts & STS_MOVING) != 0)
                flags |= EAxisStatusFlag.Moving;
            else
                flags |= EAxisStatusFlag.InPosition;
            if ((sts & STS_ALARM) != 0)
                flags |= EAxisStatusFlag.Alarm;
            if ((sts & STS_POS_LIMIT) != 0)
                flags |= EAxisStatusFlag.PositiveLimit;
            if ((sts & STS_NEG_LIMIT) != 0)
                flags |= EAxisStatusFlag.NegativeLimit;
            lock (_sync)
            {
                if ((sts & STS_HOMED) != 0 || _homeDone[axis])
                    flags |= EAxisStatusFlag.Homed;
            }
            return flags;
        }
        #endregion

        #region IO控制
        /// <summary>IO经扩展寄存器区访问：输入=基址+10000+索引，输出=基址+11000+索引（0/1），可在PLC侧映射到实际M点</summary>
        protected override EMotionResult GetInputRefactor(int index, out bool state)
        {
            state = false;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var data = _client.ReadHoldingRegisters(InvParam.SlaveId, (ushort)(InvParam.RegisterBase + 10000 + index), 1);
                if (data == null)
                    return EMotionResult.Fail;
                state = data[0] != 0;
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
                var data = _client.ReadHoldingRegisters(InvParam.SlaveId, (ushort)(InvParam.RegisterBase + 11000 + index), 1);
                if (data == null)
                    return EMotionResult.Fail;
                state = data[0] != 0;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                return _client.WriteSingleRegister(InvParam.SlaveId, (ushort)(InvParam.RegisterBase + 11000 + index), (short)(state ? 1 : 0))
                    ? EMotionResult.Success : EMotionResult.Fail;
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
                            var sts = ReadStatusWord(i);
                            if (sts == null)
                                continue;
                            bool alm = (sts.Value & STS_ALARM) != 0;
                            if (alm && !lastAlm[i])
                                RaiseAlarm(i, $"汇川控制器报警：{Param.Name}(轴{i})", sts.Value);
                            lastAlm[i] = alm;

                            if (_homing[i] && (sts.Value & STS_HOMED) != 0)
                            {
                                _homing[i] = false;
                                _homeDone[i] = true;
                            }
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
