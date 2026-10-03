// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：GoogolHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Internal、MotionControl.Runtime.Services。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Internal;
using MotionControl.Runtime.Services;

namespace MotionControl.Runtime.Vendors.Googol
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
        /// <summary>报警监控轮询间隔（毫秒）</summary>
        private const int MonitorIntervalMs = 50;
        #endregion

        private readonly object _sync = new object();
        private readonly IGoogolTransport _transport;
        private readonly AxisPollingMonitor _monitor;
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeDone = Array.Empty<bool>();
        private double[] _velOverride = Array.Empty<double>(); // <=0表示未用SetSpeed覆盖

        /// <summary>
        /// 初始化 GoogolHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public GoogolHelper()
            : this(new GoogolNativeTransport())
        {
        }

        /// <summary>
        /// 初始化 GoogolHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof、RaiseAlarm、CheckHomeDone；具体条件和先后次序见方法体。
        /// </summary>
        internal GoogolHelper(IGoogolTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _monitor = new AxisPollingMonitor(
                MonitorIntervalMs,
                ReadMonitorSample,
                (axis, rawStatus) => RaiseAlarm(axis, $"固高卡报警：{EffectiveParam.Name}(轴{axis})", rawStatus),
                (axis, _) => CheckHomeDone(axis));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ValidateParameters。
        /// 正文中的关键协作调用包括 ValidateParameters、Failure、Success；具体条件和先后次序见方法体。
        /// </summary>
        protected override MotionResult ValidateParameters()
        {
            var common = base.ValidateParameters();
            if (!common.IsSuccess)
                return common;
            if (EffectiveParam.CardNo > short.MaxValue)
                return MotionResult.Failure(EMotionResult.InvalidParam, "固高通道号必须在 0 到 32767 之间。");
            if (AxisCount > 64)
                return MotionResult.Failure(EMotionResult.InvalidParam, "固高控制卡最多支持 64 个位掩码轴。 ");
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

                    var openResult = _transport.Open((short)EffectiveParam.CardNo,
                        EffectiveParam.ConfigFile, AxisCount);
                    if (openResult != EMotionResult.Success)
                        return openResult;

                    _homing = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _velOverride = new double[AxisCount];

                    _monitor.Start(AxisCount);
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DisConnectRefactor。
        /// 正文中的关键协作调用包括 StopAllInternal、Stop、Close、Empty；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult DisConnectRefactor()
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.Success;

                StopAllInternal(EStopMode.Emergency);
                _monitor.Stop();
                _transport.Close();

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
                return _transport.SetServo(axis, true) ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ServoOffRefactor。
        /// 正文中的关键协作调用包括 SetServo；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult ServoOffRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var succeeded = _transport.SetServo(axis, false);
                if (succeeded)
                {
                    _homing[axis] = false;
                    _homeDone[axis] = false;
                }
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearAxisStatusRefactor。
        /// 正文中的关键协作调用包括 ClearStatus；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homeDone[axis] = false;
                _homing[axis] = false;
                // 清除驱动器报警标志、跟随误差越限、限位触发标志；驱动器本体ALM需按其手册复位
                return _transport.ClearStatus(axis) ? EMotionResult.Success : EMotionResult.Fail;
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsRefactor。
        /// 正文中的关键协作调用包括 CheckReady、CheckSoftLimit、StartTrapMove、ClampPulse；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult MoveAbsRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!CheckSoftLimit(axis, pulse))
                    return EMotionResult.InvalidParam;
                return StartTrapMove(axis, ClampPulse(pulse))
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveRelRefactor。
        /// 正文中的关键协作调用包括 CheckReady、TryGetCommandPosition、CheckSoftLimit、StartTrapMove、ClampPulse；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!_transport.TryGetCommandPosition(axis, out var cur))
                    return EMotionResult.Fail;
                long target = (long)cur + pulse;
                if (!CheckSoftLimit(axis, target))
                    return EMotionResult.InvalidParam;
                return StartTrapMove(axis, ClampPulse(target))
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 JogStartRefactor。
        /// 正文中的关键协作调用包括 CheckReady、AxisConfig、EffectiveVelocity、StartJog、ToAcc；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                var cfg = AxisConfig(axis);
                double vel = EffectiveVelocity(axis) * cfg.PulseEquivalent;
                return _transport.StartJog(axis, ToAcc(cfg.Accel, axis), ToAcc(cfg.Decel, axis),
                    direction > 0 ? vel : -vel)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAxisRefactor。
        /// 正文中的关键协作调用包括 StopAxis；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                _homing[axis] = false;
                return _transport.StopAxis(axis, stopMode == EStopMode.Emergency)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAllRefactor。
        /// 正文中的关键协作调用包括 StopAllInternal；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAllInternal。
        /// 正文中的关键协作调用包括 StopAll；具体条件和先后次序见方法体。
        /// </summary>
        private bool StopAllInternal(EStopMode stopMode)
        {
            var succeeded = _transport.StopAll(AxisCount, stopMode == EStopMode.Emergency);
            for (int i = 0; i < _homing.Length; i++)
                _homing[i] = false;
            return succeeded;
        }

        /// <summary>启动点位(梯形)运动</summary>
        private bool StartTrapMove(int axis, int targetPulse)
        {
            var cfg = AxisConfig(axis);
            double vel = EffectiveVelocity(axis) * cfg.PulseEquivalent;
            return _transport.StartTrapMove(axis, targetPulse, ToAcc(cfg.Accel, axis),
                ToAcc(cfg.Decel, axis), Math.Max(1, cfg.VelLow * cfg.PulseEquivalent), vel);
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClampPulse。
        /// 正文中的关键协作调用包括 Clamp；具体条件和先后次序见方法体。
        /// </summary>
        private static int ClampPulse(long pulse) => (int)Math.Clamp(pulse, int.MinValue, int.MaxValue);

        /// <summary>伺服使能状态查询（状态字bit11，异常时回退到本地跟踪）</summary>
        private bool IsServoOn(int axis)
        {
            if (!_transport.TryGetStatus(axis, out var sts))
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
                        if (!_transport.SetCurrentAsOrigin(axis))
                            return EMotionResult.Fail;
                        _homeDone[axis] = true;
                        return EMotionResult.Success;
                    default:
                        double equiv = cfg.PulseEquivalent > 0 ? cfg.PulseEquivalent : 1;
                        var profile = new GoogolHomeProfile(
                            ToGoogolHomeMode(cfg.HomeMode),
                            (short)cfg.HomeDirection,
                            Math.Max(1, cfg.HomeVelocity * equiv),
                            Math.Max(1, cfg.VelLow * equiv),
                            ToAcc(cfg.Accel, axis),
                            ToAcc(cfg.Decel, axis),
                            ClampPulse(ToPulse(axis, cfg.HomeOffset)));
                        _homing[axis] = true;
                        _homeDone[axis] = false;
                        var succeeded = _transport.StartHome(axis, profile);
                        if (!succeeded)
                            _homing[axis] = false;
                        return succeeded ? EMotionResult.Success : EMotionResult.Fail;
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
                if (!_isConnected || (uint)axis >= (uint)_homing.Length || !_homing[axis])
                    return;
                if (!_transport.TryGetHomeState(axis, out var state))
                    return;
                if (state.Stage < GTS.HOME_STAGE_END)
                    return;
                _homing[axis] = false;
                if (state.Error != GTS.HOME_ERROR_NONE)
                    return;
                _homeDone[axis] = true;

                double offset = AxisConfig(axis).HomeOffset;
                if (Math.Abs(offset) > double.Epsilon && _transport.TryGetCommandPosition(axis, out var current))
                    _transport.SetCommandPosition(axis, ClampPulse(ToPulse(axis, offset)) + (int)current);
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
                var succeeded = _transport.TryGetCommandPosition(axis, out var pos);
                pulse = (long)pos;
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetEncoderPositionRefactor。
        /// 正文中的关键协作调用包括 TryGetEncoderPosition；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                var succeeded = _transport.TryGetEncoderPosition(axis, out var pos);
                pulse = (long)pos;
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetAxisStatusRefactor。
        /// 正文中的关键协作调用包括 TryGetStatus；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (!_transport.TryGetStatus(axis, out var sts))
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
                if (!_transport.TryReadInputs(out var value))
                    return EMotionResult.Fail;
                state = (value & (1L << index)) != 0;
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetOutputRefactor。
        /// 正文中的关键协作调用包括 TryReadOutputs；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            state = false;
            lock (_sync)
            {
                if (!_isConnected || index < 0 || index > 31)
                    return !_isConnected ? EMotionResult.NotConnected : EMotionResult.InvalidParam;
                if (!_transport.TryReadOutputs(out var value))
                    return EMotionResult.Fail;
                state = (value & (1L << index)) != 0;
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetOutputRefactor。
        /// 正文中的关键协作调用包括 WriteOutput；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult SetOutputRefactor(int index, bool state)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                if (index < 0 || index > short.MaxValue)
                    return EMotionResult.InvalidParam;
                return _transport.WriteOutput(index, state)
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }
        #endregion

        #region 后台监控（报警事件 + 回零完成检测）
        private AxisMonitorSample? ReadMonitorSample(int axis)
        {
            lock (_sync)
            {
                if ((uint)axis >= (uint)_homing.Length ||
                    !_transport.TryGetStatus(axis, out var status))
                    return null;

                return new AxisMonitorSample(status, (status & GTS.GTS_STS_ALARM) != 0);
            }
        }
        #endregion
    }
}


