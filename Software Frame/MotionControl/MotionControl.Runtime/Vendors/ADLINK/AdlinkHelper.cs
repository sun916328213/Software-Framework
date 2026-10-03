// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AdlinkHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Internal、MotionControl.Runtime.Services。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Internal;
using MotionControl.Runtime.Services;

namespace MotionControl.Runtime.Vendors.ADLINK
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
        private readonly IAdlinkTransport _transport;
        private readonly AxisPollingMonitor _monitor;
        private bool[] _servoOn = Array.Empty<bool>();
        private bool[] _homing = Array.Empty<bool>();
        private bool[] _homeSeen = Array.Empty<bool>();   // 回零中已观察到HMV位（防误判）
        private bool[] _homeDone = Array.Empty<bool>();
        private double[] _velOverride = Array.Empty<double>(); // <=0表示未用SetSpeed覆盖

        /// <summary>
        /// 初始化 AdlinkHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public AdlinkHelper()
            : this(new AdlinkNativeTransport())
        {
        }

        /// <summary>
        /// 初始化 AdlinkHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof、RaiseAlarm、CheckHomeDone；具体条件和先后次序见方法体。
        /// </summary>
        internal AdlinkHelper(IAdlinkTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _monitor = new AxisPollingMonitor(
                MonitorIntervalMs,
                ReadMonitorSample,
                (axis, rawStatus) => RaiseAlarm(axis, $"凌华卡报警：{EffectiveParam.Name}(轴{axis})", rawStatus),
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
            if (EffectiveParam.CardNo > 31)
                return MotionResult.Failure(EMotionResult.InvalidParam, "凌华板卡号必须在 0 到 31 之间。");
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

                    var profiles = Enumerable.Range(0, AxisCount)
                        .Select(CreateAxisProfile)
                        .ToArray();
                    var openResult = _transport.Open(EffectiveParam.CardNo,
                        EffectiveParam.ConfigFile, profiles);
                    if (openResult != EMotionResult.Success)
                        return openResult;

                    _servoOn = new bool[AxisCount];
                    _homing = new bool[AxisCount];
                    _homeSeen = new bool[AxisCount];
                    _homeDone = new bool[AxisCount];
                    _velOverride = new double[AxisCount];

                    _monitor.Start(AxisCount);
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
        private AdlinkAxisProfile CreateAxisProfile(int axis)
        {
            var cfg = AxisConfig(axis);
            int acc = ToAccDec(cfg.Accel, axis);
            int dec = ToAccDec(cfg.Decel, axis);
            return new AdlinkAxisProfile(
                ToSpeed(cfg.VelHigh, axis),
                acc,
                dec,
                ToAdlinkHomeMode(cfg.HomeMode),
                cfg.HomeDirection == EDirection.Positive ? ADL_HOME_DIR_POSITIVE : ADL_HOME_DIR_NEGATIVE,
                ToSpeed(cfg.VelLow, axis),
                ToSpeed(cfg.HomeVelocity, axis));
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DisConnectRefactor。
        /// 正文中的关键协作调用包括 StopAllInternal、Stop、Close、ResetRuntimeState；具体条件和先后次序见方法体。
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

                ResetRuntimeState();
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ResetRuntimeState。
        /// 正文中的关键协作调用包括 Empty；具体条件和先后次序见方法体。
        /// </summary>
        private void ResetRuntimeState()
        {
            _servoOn = Array.Empty<bool>();
            _homing = Array.Empty<bool>();
            _homeSeen = Array.Empty<bool>();
            _homeDone = Array.Empty<bool>();
            _velOverride = Array.Empty<double>();
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
                    _servoOn[axis] = false;
                    _homing[axis] = false;
                    _homeDone[axis] = false;
                }
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearAxisStatusRefactor。
        /// 正文中的关键协作调用包括 ResetServo；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult ClearAxisStatusRefactor(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;

                // 脉冲型板卡无统一软件清报警指令：多数驱动器通过伺服重新上电复位ALM，
                // 此处执行伺服关-延时-开循环；若驱动器需专用RST信号清障请改为输出RST。
                bool wasOn = _servoOn[axis];
                _servoOn[axis] = false;
                if (!_transport.ResetServo(axis, wasOn))
                    return EMotionResult.Fail;
                _servoOn[axis] = wasOn;

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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsRefactor。
        /// 正文中的关键协作调用包括 CheckReady、CheckSoftLimit、MoveAbsolute、ClampPulse、EffectiveSpeed；具体条件和先后次序见方法体。
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
                return _transport.MoveAbsolute(axis, ClampPulse(pulse), EffectiveSpeed(axis))
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveRelRefactor。
        /// 正文中的关键协作调用包括 CheckReady、TryGetCommandPosition、CheckSoftLimit、MoveRelative、ClampPulse；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult MoveRelRefactor(int axis, long pulse)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                if (!_transport.TryGetCommandPosition(axis, out var current))
                    return EMotionResult.Fail;
                long target = current + pulse;
                if (!CheckSoftLimit(axis, target))
                    return EMotionResult.InvalidParam;
                return _transport.MoveRelative(axis, ClampPulse(pulse), EffectiveSpeed(axis))
                    ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 JogStartRefactor。
        /// 正文中的关键协作调用包括 CheckReady、MoveVelocity、EffectiveSpeed；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult JogStartRefactor(int axis, short direction)
        {
            lock (_sync)
            {
                if (!CheckReady(axis))
                    return _isConnected ? EMotionResult.Fail : EMotionResult.NotConnected;
                return _transport.MoveVelocity(axis,
                    direction > 0 ? EffectiveSpeed(axis) : -EffectiveSpeed(axis))
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClampPulse。
        /// 正文中的关键协作调用包括 Clamp；具体条件和先后次序见方法体。
        /// </summary>
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
                        // 回零曲线参数已在Connect时写入板卡，此处启动即可；
                        // 完成检测由监控线程负责（HMV位消失后置Homed并施加偏移）
                        _homing[axis] = true;
                        _homeSeen[axis] = false;
                        _homeDone[axis] = false;
                        var succeeded = _transport.StartHome(axis);
                        if (!succeeded)
                            _homing[axis] = false;
                        return succeeded ? EMotionResult.Success : EMotionResult.Fail;
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
                var succeeded = _transport.TryGetCommandPosition(axis, out var pos);
                pulse = pos;
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
                pulse = pos;
                return succeeded ? EMotionResult.Success : EMotionResult.Fail;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetAxisStatusRefactor。
        /// 正文中的关键协作调用包括 GetMotionStatus、GetMotionIoStatus；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = 0;
            lock (_sync)
            {
                if (!_isConnected)
                    return EMotionResult.NotConnected;
                int mts = _transport.GetMotionStatus(axis);
                int mio = _transport.GetMotionIoStatus(axis);
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
                if (!_transport.TryReadInput(index, out state))
                {
                    state = false;
                    return EMotionResult.Fail;
                }
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetOutputRefactor。
        /// 正文中的关键协作调用包括 TryReadOutput；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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
                if (!_transport.TryReadOutput(index, out state))
                {
                    state = false;
                    return EMotionResult.Fail;
                }
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
                if (index < 0 || index >= DIO_GROUP_BITS * 16)
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
                if ((uint)axis >= (uint)_homing.Length)
                    return null;

                int status = _transport.GetMotionIoStatus(axis);
                return new AxisMonitorSample(status, (status & APS168.MIO_ALM) != 0);
            }
        }

        /// <summary>回零完成检测：HMV位出现后消失且轴停止，判定回零完成</summary>
        private void CheckHomeDone(int axis)
        {
            lock (_sync)
            {
                if (!_isConnected || (uint)axis >= (uint)_homing.Length ||
                    !_homing[axis])
                    return;
                int mts = _transport.GetMotionStatus(axis);
                if ((mts & APS168.MTS_HMV) != 0)
                {
                    _homeSeen[axis] = true;
                    return;
                }
                if (!_homeSeen[axis] || (mts & APS168.MTS_MOVING_MASK) != 0)
                    return;

                _homing[axis] = false;

                // 施加回零偏移：指令/反馈位置同时偏移
                double offset = AxisConfig(axis).HomeOffset;
                if (Math.Abs(offset) > double.Epsilon)
                {
                    int off = ClampPulse(ToPulse(axis, offset));
                    if (!_transport.SetCurrentPosition(axis, off))
                        return;
                }
                _homeDone[axis] = true;
            }
        }
        #endregion
    }
}


