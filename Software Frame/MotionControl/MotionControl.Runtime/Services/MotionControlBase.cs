// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionControlBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Abstractions、MotionControl.Core.Definitions、MotionControl.Runtime.Internal。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Internal;

namespace MotionControl.Runtime.Services
{
    /// <summary>
    /// 运动控制卡抽象基类（模板方法模式）
    /// <para>公共方法负责单位换算与结果判断，厂商实现只需重写各Refactor方法（操作对象为脉冲量）</para>
    /// </summary>
    public abstract class MotionControlBase : IMotionCard
    {
        private const double Int64ExclusiveUpperBound = 9223372036854775808d;
        private readonly object _connectionSync = new();
        private MotionCardParam _configuredParam = new MotionCardParam();
        private volatile MotionCardParam? _activeParam;
        internal volatile bool _isConnected;
        /// <inheritdoc/>
        public bool IsConnected => _isConnected;

        /// <inheritdoc/>
        public virtual string Name
        {
            get => EffectiveParam.Name;
            set
            {
                lock (_connectionSync)
                    _configuredParam.Name = value;
            }
        }

        /// <inheritdoc/>
        public virtual MotionCardParam Param
        {
            get => _configuredParam;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                lock (_connectionSync)
                    _configuredParam = value;
            }
        }

        /// <summary>当前连接使用的稳定参数；未连接时返回待连接配置。</summary>
        protected MotionCardParam EffectiveParam => _activeParam ?? _configuredParam;

        /// <inheritdoc/>
        public int AxisCount => EffectiveParam.Axises?.Count ?? 0;

        /// <inheritdoc/>
        public event EventHandler<AxisAlarmEventArgs>? AlarmOccurred;

        /// <summary>
        /// 触发轴报警事件，供具体实现调用
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="message">报警信息</param>
        /// <param name="rawStatus">原始状态字</param>
        protected void RaiseAlarm(int axis, string message, long rawStatus)
        {
            AlarmOccurred?.Invoke(this, new AxisAlarmEventArgs(axis, message, rawStatus));
        }

        #region 单位换算
        /// <summary>
        /// 获取指定轴参数（含范围校验）
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        protected AxisParam AxisConfig(int axis)
        {
            if (EffectiveParam.Axises == null || axis < 0 || axis >= EffectiveParam.Axises.Count)
                throw new ArgumentOutOfRangeException(nameof(axis), $"轴索引 {axis} 超出范围（共 {AxisCount} 轴）");
            return EffectiveParam.Axises[axis];
        }

        /// <summary>
        /// 用户单位转脉冲数
        /// </summary>
        protected long ToPulse(int axis, double unit)
        {
            var pulse = Math.Round(unit * AxisConfig(axis).PulseEquivalent);
            if (!double.IsFinite(pulse) || pulse < long.MinValue || pulse >= Int64ExclusiveUpperBound)
                throw new ArgumentOutOfRangeException(nameof(unit), "数值换算为脉冲后超出 Int64 范围。");
            return (long)pulse;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryToPulse。
        /// 正文中的关键协作调用包括 IsFinite、ToPulse；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private bool TryToPulse(int axis, double unit, out long pulse)
        {
            pulse = 0;
            if (!double.IsFinite(unit))
                return false;

            try
            {
                pulse = ToPulse(axis, unit);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        /// <summary>
        /// 脉冲数转用户单位
        /// </summary>
        protected double ToUnit(int axis, long pulse) => pulse / AxisConfig(axis).PulseEquivalent;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ValidateAxis。
        /// 正文中的关键协作调用包括 Success、Failure；具体条件和先后次序见方法体。
        /// </summary>
        private MotionResult ValidateAxis(int axis)
            => axis >= 0 && axis < AxisCount
                ? MotionResult.Success()
                : MotionResult.Failure(EMotionResult.InvalidAxis, $"轴索引 {axis} 超出范围（共 {AxisCount} 轴）。");

        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 ExecuteAxisCommand。
        /// 正文中的关键协作调用包括 ValidateAxis、ExecuteConnectedCommand；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        private MotionResult ExecuteAxisCommand(int axis, string operation, Func<EMotionResult> command)
        {
            lock (_connectionSync)
            {
                var validation = ValidateAxis(axis);
                return validation.IsSuccess
                    ? ExecuteConnectedCommand(operation, command)
                    : validation;
            }
        }

        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 ExecuteConnectedCommand。
        /// 正文中的关键协作调用包括 Failure、command、Success；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        private MotionResult ExecuteConnectedCommand(string operation, Func<EMotionResult> command)
        {
            lock (_connectionSync)
            {
                if (!_isConnected)
                    return MotionResult.Failure(EMotionResult.NotConnected, $"{operation}失败：控制卡未连接。");

                var result = command();
                return result == EMotionResult.Success
                    ? MotionResult.Success()
                    : MotionResult.Failure(result, $"{operation}失败（{result}）。");
            }
        }
        #endregion

        #region 连接/断开
        /// <inheritdoc/>
        public bool Connect()
            => ConnectResult().IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 发起连接操作；对应入口为 ConnectResult。
        /// 正文中的关键协作调用包括 Success、Snapshot、ValidateParameters、ConnectRefactor、Failure；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public MotionResult ConnectResult()
        {
            lock (_connectionSync)
            {
                if (_isConnected)
                    return MotionResult.Success();

                _activeParam = _configuredParam.Snapshot();
                var validation = ValidateParameters();
                if (!validation.IsSuccess)
                {
                    _activeParam = null;
                    return validation;
                }

                try
                {
                    var result = ConnectRefactor();
                    _isConnected = result == EMotionResult.Success;
                    if (!_isConnected)
                        _activeParam = null;
                    return _isConnected
                        ? MotionResult.Success()
                        : MotionResult.Failure(result, "控制卡连接失败。");
                }
                catch
                {
                    _activeParam = null;
                    throw;
                }
            }
        }

        /// <summary>连接前参数校验；厂商实现可在通用规则之上追加约束。</summary>
        protected virtual MotionResult ValidateParameters()
            => MotionParameterValidator.Validate(EffectiveParam);

        /// <summary>
        /// 连接控制卡（打开卡）
        /// </summary>
        protected abstract EMotionResult ConnectRefactor();

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 DisConnect。
        /// 正文中的关键协作调用包括 DisconnectResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool DisConnect() => DisconnectResult().IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 执行断开连接操作；对应入口为 DisconnectResult。
        /// 正文中的关键协作调用包括 Success、DisConnectRefactor、Failure；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult DisconnectResult()
        {
            lock (_connectionSync)
            {
                if (!_isConnected)
                    return MotionResult.Success();

                var result = DisConnectRefactor();
                _isConnected = result != EMotionResult.Success;
                if (!_isConnected)
                    _activeParam = null;
                return !_isConnected
                    ? MotionResult.Success()
                    : MotionResult.Failure(result, $"控制卡断开失败（{result}）。");
            }
        }
        /// <summary>
        /// 断开控制卡（关闭卡）
        /// </summary>
        protected abstract EMotionResult DisConnectRefactor();
        #endregion

        #region 伺服控制
        /// <inheritdoc/>
        public bool ServoOn(int axis) => ServoOnResult(axis).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ServoOnResult。
        /// 正文中的关键协作调用包括 ExecuteAxisCommand、ServoOnRefactor；具体条件和先后次序见方法体。
        /// </remarks>
        public MotionResult ServoOnResult(int axis)
            => ExecuteAxisCommand(axis, "伺服使能", () => ServoOnRefactor(axis));
        /// <summary>
        /// 伺服使能
        /// </summary>
        protected abstract EMotionResult ServoOnRefactor(int axis);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ServoOff。
        /// 正文中的关键协作调用包括 ServoOffResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool ServoOff(int axis) => ServoOffResult(axis).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ServoOffResult。
        /// 正文中的关键协作调用包括 ExecuteAxisCommand、ServoOffRefactor；具体条件和先后次序见方法体。
        /// </remarks>
        public MotionResult ServoOffResult(int axis)
            => ExecuteAxisCommand(axis, "伺服失能", () => ServoOffRefactor(axis));
        /// <summary>
        /// 伺服失能
        /// </summary>
        protected abstract EMotionResult ServoOffRefactor(int axis);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ClearAxisStatus。
        /// 正文中的关键协作调用包括 ClearAxisStatusResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool ClearAxisStatus(int axis) => ClearAxisStatusResult(axis).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ClearAxisStatusResult。
        /// 正文中的关键协作调用包括 ExecuteAxisCommand、ClearAxisStatusRefactor；具体条件和先后次序见方法体。
        /// </remarks>
        public MotionResult ClearAxisStatusResult(int axis)
            => ExecuteAxisCommand(axis, "清除轴状态", () => ClearAxisStatusRefactor(axis));
        /// <summary>
        /// 清除轴状态（报警、限位等）
        /// </summary>
        protected abstract EMotionResult ClearAxisStatusRefactor(int axis);
        #endregion

        #region 运动控制
        /// <inheritdoc/>
        public bool SetSpeed(int axis, double velocity)
            => SetSpeedResult(axis, velocity).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 设置当前实现管理的值或状态；对应入口为 SetSpeedResult。
        /// 正文中的关键协作调用包括 ValidateAxis、TryToPulse、Failure、ExecuteConnectedCommand、SetSpeedRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult SetSpeedResult(int axis, double velocity)
        {
            lock (_connectionSync)
            {
                var axisValidation = ValidateAxis(axis);
                if (!axisValidation.IsSuccess)
                    return axisValidation;
                if (velocity <= 0 || !TryToPulse(axis, velocity, out long pulse) || pulse <= 0)
                    return MotionResult.Failure(EMotionResult.InvalidParam, "速度必须是可换算为正脉冲数的有限正数。");
                return ExecuteConnectedCommand("设置轴速度", () => SetSpeedRefactor(axis, pulse));
            }
        }
        /// <summary>
        /// 设置轴运行速度
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="velPulse">速度（脉冲/s）</param>
        protected abstract EMotionResult SetSpeedRefactor(int axis, long velPulse);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 MoveAbs。
        /// 正文中的关键协作调用包括 MoveAbsResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool MoveAbs(int axis, double position)
            => MoveAbsResult(axis, position).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsResult。
        /// 正文中的关键协作调用包括 ValidateAxis、TryToPulse、Failure、ExecuteConnectedCommand、MoveAbsRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult MoveAbsResult(int axis, double position)
        {
            lock (_connectionSync)
            {
                var axisValidation = ValidateAxis(axis);
                if (!axisValidation.IsSuccess)
                    return axisValidation;
                if (!TryToPulse(axis, position, out long pulse))
                    return MotionResult.Failure(EMotionResult.InvalidParam, "目标位置必须是可换算为脉冲数的有限数值。");
                return ExecuteConnectedCommand("绝对运动", () => MoveAbsRefactor(axis, pulse));
            }
        }
        /// <summary>
        /// 绝对运动到目标位置
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">目标位置（脉冲）</param>
        protected abstract EMotionResult MoveAbsRefactor(int axis, long pulse);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 MoveRel。
        /// 正文中的关键协作调用包括 MoveRelResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool MoveRel(int axis, double distance)
            => MoveRelResult(axis, distance).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 MoveRelResult。
        /// 正文中的关键协作调用包括 ValidateAxis、TryToPulse、Failure、ExecuteConnectedCommand、MoveRelRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult MoveRelResult(int axis, double distance)
        {
            lock (_connectionSync)
            {
                var axisValidation = ValidateAxis(axis);
                if (!axisValidation.IsSuccess)
                    return axisValidation;
                if (!TryToPulse(axis, distance, out long pulse))
                    return MotionResult.Failure(EMotionResult.InvalidParam, "相对距离必须是可换算为脉冲数的有限数值。");
                return ExecuteConnectedCommand("相对运动", () => MoveRelRefactor(axis, pulse));
            }
        }
        /// <summary>
        /// 相对当前位置运动
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">运动距离（脉冲，负值表示负方向）</param>
        protected abstract EMotionResult MoveRelRefactor(int axis, long pulse);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 JogStart。
        /// 正文中的关键协作调用包括 JogStartResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool JogStart(int axis, EDirection direction) => JogStartResult(axis, direction).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 JogStartResult。
        /// 正文中的关键协作调用包括 ValidateAxis、IsDefined、Failure、ExecuteConnectedCommand、JogStartRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult JogStartResult(int axis, EDirection direction)
        {
            lock (_connectionSync)
            {
                var axisValidation = ValidateAxis(axis);
                if (!axisValidation.IsSuccess)
                    return axisValidation;
                if (!Enum.IsDefined(direction))
                    return MotionResult.Failure(EMotionResult.InvalidParam, "点动方向无效。");
                return ExecuteConnectedCommand("开始点动", () => JogStartRefactor(axis, (short)direction));
            }
        }
        /// <summary>
        /// 开始点动
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="direction">方向：-1负向 1正向</param>
        protected abstract EMotionResult JogStartRefactor(int axis, short direction);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 Stop。
        /// 正文中的关键协作调用包括 StopResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool Stop(int axis, EStopMode stopMode = EStopMode.Decelerate)
            => StopResult(axis, stopMode).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 StopResult。
        /// 正文中的关键协作调用包括 ValidateAxis、IsDefined、Failure、ExecuteConnectedCommand、StopAxisRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult StopResult(int axis, EStopMode stopMode = EStopMode.Decelerate)
        {
            lock (_connectionSync)
            {
                var axisValidation = ValidateAxis(axis);
                if (!axisValidation.IsSuccess)
                    return axisValidation;
                if (!Enum.IsDefined(stopMode))
                    return MotionResult.Failure(EMotionResult.InvalidParam, "停止方式无效。");
                return ExecuteConnectedCommand("停止单轴", () => StopAxisRefactor(axis, stopMode));
            }
        }
        /// <summary>
        /// 停止单轴
        /// </summary>
        protected abstract EMotionResult StopAxisRefactor(int axis, EStopMode stopMode);

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 StopAll。
        /// 正文中的关键协作调用包括 StopAllResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool StopAll(EStopMode stopMode = EStopMode.Emergency)
            => StopAllResult(stopMode).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 StopAllResult。
        /// 正文中的关键协作调用包括 IsDefined、Failure、ExecuteConnectedCommand、StopAllRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult StopAllResult(EStopMode stopMode = EStopMode.Emergency)
        {
            lock (_connectionSync)
            {
                if (!Enum.IsDefined(stopMode))
                    return MotionResult.Failure(EMotionResult.InvalidParam, "停止方式无效。");
                return ExecuteConnectedCommand("停止全部轴", () => StopAllRefactor(stopMode));
            }
        }
        /// <summary>
        /// 停止本卡所有轴
        /// </summary>
        protected abstract EMotionResult StopAllRefactor(EStopMode stopMode);
        #endregion

        #region 回零
        /// <inheritdoc/>
        public bool StartHome(int axis) => StartHomeResult(axis).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 StartHomeResult。
        /// 正文中的关键协作调用包括 ExecuteAxisCommand、HomeStartRefactor；具体条件和先后次序见方法体。
        /// </remarks>
        public MotionResult StartHomeResult(int axis)
            => ExecuteAxisCommand(axis, "启动回零", () => HomeStartRefactor(axis));
        /// <summary>
        /// 启动回零
        /// </summary>
        protected abstract EMotionResult HomeStartRefactor(int axis);
        #endregion

        #region 状态查询
        /// <inheritdoc/>
        public double GetCommandPosition(int axis)
        {
            lock (_connectionSync)
            {
                AxisConfig(axis);
                var iRet = GetCommandPositionRefactor(axis, out long pulse);
                return iRet == EMotionResult.Success ? ToUnit(axis, pulse) : 0;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 读取当前实现提供的数据；对应入口为 ReadCommandPosition。
        /// 正文中的关键协作调用包括 AxisConfig、Failure、GetCommandPositionRefactor、Success、ToUnit；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public MotionResult<double> ReadCommandPosition(int axis)
        {
            lock (_connectionSync)
            {
                try
                {
                    AxisConfig(axis);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return MotionResult<double>.Failure(EMotionResult.InvalidAxis, ex.Message);
                }

                var result = GetCommandPositionRefactor(axis, out long pulse);
                return result == EMotionResult.Success
                    ? MotionResult<double>.Success(ToUnit(axis, pulse))
                    : MotionResult<double>.Failure(result, $"读取轴 {axis} 指令位置失败。");
            }
        }
        /// <summary>
        /// 获取指令位置
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">指令位置（脉冲）</param>
        protected abstract EMotionResult GetCommandPositionRefactor(int axis, out long pulse);

        /// <inheritdoc/>
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetEncoderPosition。
        /// 正文中的关键协作调用包括 AxisConfig、GetEncoderPositionRefactor、ToUnit；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public double GetEncoderPosition(int axis)
        {
            lock (_connectionSync)
            {
                AxisConfig(axis);
                var iRet = GetEncoderPositionRefactor(axis, out long pulse);
                return iRet == EMotionResult.Success ? ToUnit(axis, pulse) : 0;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 读取当前实现提供的数据；对应入口为 ReadEncoderPosition。
        /// 正文中的关键协作调用包括 AxisConfig、Failure、GetEncoderPositionRefactor、Success、ToUnit；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public MotionResult<double> ReadEncoderPosition(int axis)
        {
            lock (_connectionSync)
            {
                try
                {
                    AxisConfig(axis);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return MotionResult<double>.Failure(EMotionResult.InvalidAxis, ex.Message);
                }

                var result = GetEncoderPositionRefactor(axis, out long pulse);
                return result == EMotionResult.Success
                    ? MotionResult<double>.Success(ToUnit(axis, pulse))
                    : MotionResult<double>.Failure(result, $"读取轴 {axis} 编码器位置失败。");
            }
        }
        /// <summary>
        /// 获取编码器位置
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="pulse">编码器位置（脉冲）</param>
        protected abstract EMotionResult GetEncoderPositionRefactor(int axis, out long pulse);

        /// <inheritdoc/>
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetAxisStatus。
        /// 正文中的关键协作调用包括 AxisConfig、GetAxisStatusRefactor、ParseAxisFlags；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public AxisStatus GetAxisStatus(int axis)
        {
            lock (_connectionSync)
            {
                AxisConfig(axis);
                var iRet = GetAxisStatusRefactor(axis, out long raw);
                if (iRet != EMotionResult.Success)
                    return new AxisStatus { Axis = axis };
                return new AxisStatus { Axis = axis, Raw = raw, Flags = ParseAxisFlags(axis, raw) };
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 读取当前实现提供的数据；对应入口为 ReadAxisStatus。
        /// 正文中的关键协作调用包括 AxisConfig、Failure、GetAxisStatusRefactor、Success、ParseAxisFlags；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public MotionResult<AxisStatus> ReadAxisStatus(int axis)
        {
            lock (_connectionSync)
            {
                try
                {
                    AxisConfig(axis);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return MotionResult<AxisStatus>.Failure(EMotionResult.InvalidAxis, ex.Message);
                }

                var result = GetAxisStatusRefactor(axis, out long raw);
                if (result != EMotionResult.Success)
                    return MotionResult<AxisStatus>.Failure(result, $"读取轴 {axis} 状态失败。");

                return MotionResult<AxisStatus>.Success(new AxisStatus
                {
                    Axis = axis,
                    Raw = raw,
                    Flags = ParseAxisFlags(axis, raw),
                });
            }
        }
        /// <summary>
        /// 获取轴原始状态字
        /// </summary>
        /// <param name="axis">轴索引</param>
        /// <param name="raw">原始状态字</param>
        protected abstract EMotionResult GetAxisStatusRefactor(int axis, out long raw);

        /// <summary>
        /// 将厂商原始状态字映射为通用状态标志，默认按EAxisStatusFlag位定义解析，厂商可重写
        /// </summary>
        protected virtual EAxisStatusFlag ParseAxisFlags(int axis, long raw)
        {
            return (EAxisStatusFlag)raw;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 IsMoving。
        /// 正文中的关键协作调用包括 GetAxisStatus；具体条件和先后次序见方法体。
        /// </remarks>
        public bool IsMoving(int axis)
        {
            return GetAxisStatus(axis).Moving;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 IsInPosition。
        /// 正文中的关键协作调用包括 GetAxisStatus；具体条件和先后次序见方法体。
        /// </remarks>
        public bool IsInPosition(int axis)
        {
            return GetAxisStatus(axis).InPosition;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 IsHomeDone。
        /// 正文中的关键协作调用包括 GetAxisStatus；具体条件和先后次序见方法体。
        /// </remarks>
        public bool IsHomeDone(int axis)
        {
            return GetAxisStatus(axis).Homed;
        }
        #endregion

        #region IO控制
        /// <inheritdoc/>
        public bool GetInput(int index)
        {
            lock (_connectionSync)
            {
                var iRet = GetInputRefactor(index, out bool state);
                return iRet == EMotionResult.Success && state;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 读取当前实现提供的数据；对应入口为 ReadInput。
        /// 正文中的关键协作调用包括 GetInputRefactor、Success、Failure；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult<bool> ReadInput(int index)
        {
            lock (_connectionSync)
            {
                var result = GetInputRefactor(index, out bool state);
                return result == EMotionResult.Success
                    ? MotionResult<bool>.Success(state)
                    : MotionResult<bool>.Failure(result, $"读取输入点 {index} 失败。");
            }
        }
        /// <summary>
        /// 读取通用输入点
        /// </summary>
        protected abstract EMotionResult GetInputRefactor(int index, out bool state);

        /// <inheritdoc/>
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetOutput。
        /// 正文中的关键协作调用包括 GetOutputRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public bool GetOutput(int index)
        {
            lock (_connectionSync)
            {
                var iRet = GetOutputRefactor(index, out bool state);
                return iRet == EMotionResult.Success && state;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 读取当前实现提供的数据；对应入口为 ReadOutput。
        /// 正文中的关键协作调用包括 GetOutputRefactor、Success、Failure；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult<bool> ReadOutput(int index)
        {
            lock (_connectionSync)
            {
                var result = GetOutputRefactor(index, out bool state);
                return result == EMotionResult.Success
                    ? MotionResult<bool>.Success(state)
                    : MotionResult<bool>.Failure(result, $"读取输出点 {index} 失败。");
            }
        }
        /// <summary>
        /// 读取通用输出点当前状态
        /// </summary>
        protected abstract EMotionResult GetOutputRefactor(int index, out bool state);

        /// <inheritdoc/>
        /// <remarks>
        /// 设置当前实现管理的值或状态；对应入口为 SetOutput。
        /// 正文中的关键协作调用包括 SetOutputResult；具体条件和先后次序见方法体。
        /// </remarks>
        public bool SetOutput(int index, bool state) => SetOutputResult(index, state).IsSuccess;

        /// <inheritdoc/>
        /// <remarks>
        /// 设置当前实现管理的值或状态；对应入口为 SetOutputResult。
        /// 正文中的关键协作调用包括 Failure、ExecuteConnectedCommand、SetOutputRefactor；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </remarks>
        public MotionResult SetOutputResult(int index, bool state)
        {
            lock (_connectionSync)
            {
                if (index < 0)
                    return MotionResult.Failure(EMotionResult.InvalidParam, "输出点索引不能小于 0。");
                return ExecuteConnectedCommand($"写输出点 {index}", () => SetOutputRefactor(index, state));
            }
        }
        /// <summary>
        /// 写通用输出点
        /// </summary>
        protected abstract EMotionResult SetOutputRefactor(int index, bool state);
        #endregion

        #region 同步等待
        /// <inheritdoc/>
        public async Task<bool> WaitInPositionAsync(int axis, int timeoutMs = 5000)
        {
            return (await WaitInPositionResultAsync(axis, timeoutMs)).IsSuccess;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 WaitInPositionResultAsync。
        /// 正文中的关键协作调用包括 Failure、AxisConfig、StartNew、ReadAxisStatus、Success；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public async Task<MotionResult> WaitInPositionResultAsync(int axis, int timeoutMs = 5000,
            CancellationToken cancellationToken = default)
        {
            if (timeoutMs <= 0)
                return MotionResult.Failure(EMotionResult.InvalidParam, "超时时间必须大于 0。");

            try
            {
                AxisConfig(axis);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return MotionResult.Failure(EMotionResult.InvalidAxis, ex.Message);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return MotionResult.Failure(EMotionResult.Cancelled, "等待轴到位操作已取消。");

                var statusResult = ReadAxisStatus(axis);
                if (!statusResult.IsSuccess)
                    return MotionResult.Failure(statusResult.Code, statusResult.Message, statusResult.VendorCode);
                var status = statusResult.Value;
                if (status.Alarm || status.EmergencyStop)
                    return status.EmergencyStop
                        ? MotionResult.Failure(EMotionResult.EmergencyStop, $"轴 {axis} 触发急停。")
                        : MotionResult.Failure(EMotionResult.Alarm, $"轴 {axis} 发生报警。");
                if (!status.Moving && status.InPosition)
                    return MotionResult.Success();
                try
                {
                    await Task.Delay(10, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return MotionResult.Failure(EMotionResult.Cancelled, "等待轴到位操作已取消。");
                }
            }
            return MotionResult.Failure(EMotionResult.Timeout, $"等待轴 {axis} 到位超时（{timeoutMs}ms）。");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 WaitHomeDoneAsync。
        /// 正文中的关键协作调用包括 WaitHomeDoneResultAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </remarks>
        public async Task<bool> WaitHomeDoneAsync(int axis, int timeoutMs = 30000)
        {
            return (await WaitHomeDoneResultAsync(axis, timeoutMs)).IsSuccess;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 WaitHomeDoneResultAsync。
        /// 正文中的关键协作调用包括 Failure、AxisConfig、StartNew、ReadAxisStatus、Success；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public async Task<MotionResult> WaitHomeDoneResultAsync(int axis, int timeoutMs = 30000,
            CancellationToken cancellationToken = default)
        {
            if (timeoutMs <= 0)
                return MotionResult.Failure(EMotionResult.InvalidParam, "超时时间必须大于 0。");

            try
            {
                AxisConfig(axis);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return MotionResult.Failure(EMotionResult.InvalidAxis, ex.Message);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return MotionResult.Failure(EMotionResult.Cancelled, "等待回零操作已取消。");

                var statusResult = ReadAxisStatus(axis);
                if (!statusResult.IsSuccess)
                    return MotionResult.Failure(statusResult.Code, statusResult.Message, statusResult.VendorCode);
                var status = statusResult.Value;
                if (status.Alarm || status.EmergencyStop)
                    return status.EmergencyStop
                        ? MotionResult.Failure(EMotionResult.EmergencyStop, $"轴 {axis} 回零时触发急停。")
                        : MotionResult.Failure(EMotionResult.Alarm, $"轴 {axis} 回零时发生报警。");
                if (status.Homed && !status.Moving)
                    return MotionResult.Success();
                try
                {
                    await Task.Delay(10, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return MotionResult.Failure(EMotionResult.Cancelled, "等待回零操作已取消。");
                }
            }
            return MotionResult.Failure(EMotionResult.Timeout, $"等待轴 {axis} 回零超时（{timeoutMs}ms）。");
        }
        #endregion
    }
}



