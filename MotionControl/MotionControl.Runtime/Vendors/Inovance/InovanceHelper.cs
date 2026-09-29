using MotionControl.Core.Definitions;
using MotionControl.Runtime.Internal;
using MotionControl.Runtime.Services;

namespace MotionControl.Runtime.Vendors.Inovance
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
        private const int InputRegisterOffset = 10000;
        private const int OutputRegisterOffset = 11000;
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        private readonly IInovanceTransport _transport;
        private readonly AxisPollingMonitor _monitor;
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeDone = Array.Empty<bool>();
        private double[] _velOverride = Array.Empty<double>();

        public InovanceHelper()
            : this(new InovanceModbusTransport())
        {
        }

        internal InovanceHelper(IInovanceTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _monitor = new AxisPollingMonitor(
                MonitorIntervalMs,
                ReadMonitorSample,
                (axis, rawStatus) => RaiseAlarm(axis, $"汇川控制器报警：{EffectiveParam.Name}(轴{axis})", rawStatus),
                ObserveMonitorSample);
        }

        /// <summary>强类型参数（含寄存器映射）</summary>
        private InovanceParam InvParam => EffectiveParam as InovanceParam ?? new InovanceParam();

        protected override MotionResult ValidateParameters()
        {
            var common = base.ValidateParameters();
            if (!common.IsSuccess)
                return common;
            if (string.IsNullOrWhiteSpace(EffectiveParam.IpAddress))
                return MotionResult.Failure(EMotionResult.InvalidParam, "汇川控制器 IP 地址不能为空。");
            if (EffectiveParam.TcpPort <= 0 || EffectiveParam.TcpPort > ushort.MaxValue)
                return MotionResult.Failure(EMotionResult.InvalidParam, "汇川控制器 TCP 端口必须在 1 到 65535 之间。");

            var parameter = InvParam;
            if (parameter.RegisterStride < 8)
                return MotionResult.Failure(EMotionResult.InvalidParam, "汇川轴寄存器跨度不能小于 8。");
            var lastRegister = (long)parameter.RegisterBase + (AxisCount - 1L) * parameter.RegisterStride + 7;
            if (lastRegister > ushort.MaxValue)
                return MotionResult.Failure(EMotionResult.InvalidParam, "汇川轴寄存器映射超出 16 位地址范围。");
            return MotionResult.Success();
        }

        #region 连接/断开
        protected override EMotionResult ConnectRefactor()
        {
            lock (_sync)
            {
                if (_isConnected)
                    return EMotionResult.Success;
                if (AxisCount <= 0)
                    return EMotionResult.InvalidParam;
                if (string.IsNullOrWhiteSpace(EffectiveParam.IpAddress))
                    return EMotionResult.InvalidParam;

                if (!_transport.Connect(EffectiveParam.IpAddress, EffectiveParam.TcpPort, InvParam, AxisCount))
                    return EMotionResult.Fail;

                _homing = new bool[AxisCount];
                _homeDone = new bool[AxisCount];
                _velOverride = new double[AxisCount];
                _monitor.Start(AxisCount);
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
                _monitor.Stop();
                _transport.Disconnect();
                _homing = Array.Empty<bool>();
                _homeDone = Array.Empty<bool>();
                _velOverride = Array.Empty<double>();
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
                var clearResult = SetControlBits(axis, 0, CTL_CLEAR_ALARM);
                _homeDone[axis] = false;
                _homing[axis] = false;
                return ret == EMotionResult.Success ? clearResult : ret;
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
                if (!WriteTarget(axis, pulse, EffectiveVelocity(axis)))
                    return EMotionResult.Fail;
                return SetControlBits(axis, CTL_MOVE_ABS, CTL_MOVE_ABS | CTL_JOG_POSITIVE | CTL_JOG_NEGATIVE | CTL_HOME_START);
            }
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!TryReadCurrentPosition(axis, out long cur))
                    return EMotionResult.Fail;
                if (!CheckSoftLimit(axis, cur + pulse))
                    return EMotionResult.InvalidParam;
                if (!WriteTarget(axis, pulse, EffectiveVelocity(axis)))
                    return EMotionResult.Fail;
                return SetControlBits(axis, CTL_MOVE_REL, CTL_MOVE_REL | CTL_JOG_POSITIVE | CTL_JOG_NEGATIVE | CTL_HOME_START);
            }
        }

        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!WriteTarget(axis, 0, EffectiveVelocity(axis)))
                    return EMotionResult.Fail;
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
                return StopAllInternal()
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        private bool StopAllInternal()
        {
            var allSucceeded = true;
            for (int i = 0; i < AxisCount; i++)
            {
                _homing[i] = false;
                allSucceeded &= SetControlBits(i, CTL_STOP, CTL_STOP) == EMotionResult.Success;
            }
            return allSucceeded;
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
            return _velOverride[axis] > 0 ? _velOverride[axis] : cfg.VelHigh;
        }

        /// <summary>读-改-写控制字：setBits置位，mask中的位先清零</summary>
        private EMotionResult SetControlBits(int axis, ushort setBits, ushort clearMask)
        {
            return _transport.UpdateControlWord(axis, setBits, clearMask)
                ? EMotionResult.Success : EMotionResult.Fail;
        }

        /// <summary>写目标位置与目标速度</summary>
        private bool WriteTarget(int axis, long position, double velocity)
        {
            int pos = (int)Math.Clamp(position * 1.0, int.MinValue, int.MaxValue);
            return _transport.WriteTarget(axis, pos, (float)velocity);
        }

        /// <summary>读取当前反馈位置（pulse）。</summary>
        private bool TryReadCurrentPosition(int axis, out long position)
        {
            position = 0;
            if (!_transport.TryReadCurrentPosition(axis, out var value))
                return false;
            position = value;
            return true;
        }

        /// <summary>读取状态字，失败返回null</summary>
        private ushort? ReadStatusWord(int axis)
        {
            return _transport.TryReadStatus(axis, out var status) ? status : null;
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
                        if (!_transport.SetCurrentPosition(axis, 0))
                            return EMotionResult.Fail;
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
                return TryReadCurrentPosition(axis, out pulse)
                    ? EMotionResult.Success
                    : EMotionResult.Fail;
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
            if (!IsIoIndexValid(index, InputRegisterOffset))
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (!_transport.TryReadInput(index, out state))
                    return EMotionResult.Fail;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            state = false;
            if (!IsIoIndexValid(index, OutputRegisterOffset))
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (!_transport.TryReadOutput(index, out state))
                    return EMotionResult.Fail;
                return EMotionResult.Success;
            }
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            if (!IsIoIndexValid(index, OutputRegisterOffset))
                return EMotionResult.InvalidParam;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                return _transport.WriteOutput(index, state)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        private bool IsIoIndexValid(int index, int offset)
            => index >= 0 && (long)InvParam.RegisterBase + offset + index <= ushort.MaxValue;
        #endregion

        #region 后台监控（报警事件 + 回零完成检测）
        private AxisMonitorSample? ReadMonitorSample(int axis)
        {
            lock (_sync)
            {
                if ((uint)axis >= (uint)_homing.Length)
                    return null;

                var status = ReadStatusWord(axis);
                return status.HasValue
                    ? new AxisMonitorSample(status.Value, (status.Value & STS_ALARM) != 0)
                    : null;
            }
        }

        private void ObserveMonitorSample(int axis, long rawStatus)
        {
            lock (_sync)
            {
                if ((uint)axis >= (uint)_homing.Length ||
                    (uint)axis >= (uint)_homeDone.Length ||
                    !_homing[axis] || (((ushort)rawStatus & STS_HOMED) == 0))
                    return;

                _homing[axis] = false;
                _homeDone[axis] = true;
            }
        }
        #endregion
    }
}


