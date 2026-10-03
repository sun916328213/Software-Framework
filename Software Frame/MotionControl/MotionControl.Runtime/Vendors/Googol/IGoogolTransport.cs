// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：GoogolHomeProfile、GoogolHomeState、IGoogolTransport、GoogolNativeTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;

namespace MotionControl.Runtime.Vendors.Googol
{
    /// <summary>
    /// GoogolHomeProfile 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
    /// </summary>
    internal readonly record struct GoogolHomeProfile(
        short Mode,
        short Direction,
        double HighVelocity,
        double LowVelocity,
        double Acceleration,
        double Deceleration,
        int OffsetPulse);

    /// <summary>
    /// GoogolHomeState 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
    /// </summary>
    internal readonly record struct GoogolHomeState(short Stage, short Error);

    /// <summary>固高 SDK seam；隐藏1基轴号、位掩码、多步运动提交和进程级库生命周期。</summary>
    internal interface IGoogolTransport : IDisposable
    {
        bool IsOpen { get; }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Open。
        /// </summary>
        EMotionResult Open(short channel, string? configFile, int requiredAxes);
        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// </summary>
        void Close();
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetServo。
        /// </summary>
        bool SetServo(int axis, bool enabled);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearStatus。
        /// </summary>
        bool ClearStatus(int axis);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartTrapMove。
        /// </summary>
        bool StartTrapMove(int axis, int targetPulse, double acceleration, double deceleration,
            double startVelocity, double maximumVelocity);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartJog。
        /// </summary>
        bool StartJog(int axis, double acceleration, double deceleration, double velocity);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAxis。
        /// </summary>
        bool StopAxis(int axis, bool emergency);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAll。
        /// </summary>
        bool StopAll(int axisCount, bool emergency);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetStatus。
        /// </summary>
        bool TryGetStatus(int axis, out int status);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetCommandPosition。
        /// </summary>
        bool TryGetCommandPosition(int axis, out double pulse);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetEncoderPosition。
        /// </summary>
        bool TryGetEncoderPosition(int axis, out double pulse);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentAsOrigin。
        /// </summary>
        bool SetCurrentAsOrigin(int axis);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCommandPosition。
        /// </summary>
        bool SetCommandPosition(int axis, int pulse);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartHome。
        /// </summary>
        bool StartHome(int axis, GoogolHomeProfile profile);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetHomeState。
        /// </summary>
        bool TryGetHomeState(int axis, out GoogolHomeState state);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadInputs。
        /// </summary>
        bool TryReadInputs(out long value);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadOutputs。
        /// </summary>
        bool TryReadOutputs(out long value);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// </summary>
        bool WriteOutput(int index, bool state);
    }

    /// <summary>通过 gts.dll 调用真实控制器的生产适配器。</summary>
    internal sealed class GoogolNativeTransport : IGoogolTransport
    {
        private static readonly object LibrarySync = new();
        private static readonly HashSet<short> OpenedChannels = new();
        private short _channel;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Open。
        /// 正文中的关键协作调用包括 Contains、GT_Open、IsNullOrWhiteSpace、GT_LoadConfig、CloseFailedOpen；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public EMotionResult Open(short channel, string? configFile, int requiredAxes)
        {
            lock (LibrarySync)
            {
                if (IsOpen)
                    return EMotionResult.Success;
                if (requiredAxes <= 0 || requiredAxes > 64 || OpenedChannels.Contains(channel))
                    return EMotionResult.InvalidParam;

                var hadOpenChannels = OpenedChannels.Count > 0;
                if (GTS.GT_Open(channel, 1) != 0)
                    return EMotionResult.Fail;

                if (!string.IsNullOrWhiteSpace(configFile) && GTS.GT_LoadConfig(configFile) != 0)
                {
                    CloseFailedOpen(hadOpenChannels);
                    return EMotionResult.Fail;
                }

                for (var axis = 0; axis < requiredAxes; axis++)
                {
                    if (GTS.GT_ClrSts(ToAxisNumber(axis), 1) == 0)
                        continue;

                    CloseFailedOpen(hadOpenChannels);
                    return EMotionResult.Fail;
                }

                _channel = channel;
                OpenedChannels.Add(channel);
                IsOpen = true;
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// 正文中的关键协作调用包括 Remove、GT_Close；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Close()
        {
            lock (LibrarySync)
            {
                if (!IsOpen)
                    return;

                IsOpen = false;
                OpenedChannels.Remove(_channel);
                if (OpenedChannels.Count == 0)
                    GTS.GT_Close();
            }
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetServo。
        /// 正文中的关键协作调用包括 GT_AxisOn、ToAxisNumber、GT_AxisOff；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetServo(int axis, bool enabled)
            => (enabled ? GTS.GT_AxisOn(ToAxisNumber(axis)) : GTS.GT_AxisOff(ToAxisNumber(axis))) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearStatus。
        /// 正文中的关键协作调用包括 GT_ClrSts、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool ClearStatus(int axis) => GTS.GT_ClrSts(ToAxisNumber(axis), 1) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartTrapMove。
        /// 正文中的关键协作调用包括 ToAxisNumber、GT_PrfTrap、GT_SetTrapPrm、GT_SetPos、GT_SetVel；具体条件和先后次序见方法体。
        /// </summary>
        public bool StartTrapMove(int axis, int targetPulse, double acceleration, double deceleration,
            double startVelocity, double maximumVelocity)
        {
            var axisNumber = ToAxisNumber(axis);
            var parameters = new GTS.TTrapPrm
            {
                acc = acceleration,
                dec = deceleration,
                velStart = startVelocity,
                smoothTime = 0,
            };
            return GTS.GT_PrfTrap(axisNumber) == 0 &&
                GTS.GT_SetTrapPrm(axisNumber, ref parameters) == 0 &&
                GTS.GT_SetPos(axisNumber, targetPulse) == 0 &&
                GTS.GT_SetVel(axisNumber, maximumVelocity) == 0 &&
                GTS.GT_Update(ToAxisMask(axis)) == 0;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartJog。
        /// 正文中的关键协作调用包括 ToAxisNumber、GT_PrfJog、GT_SetJogPrm、GT_SetVel、GT_Update；具体条件和先后次序见方法体。
        /// </summary>
        public bool StartJog(int axis, double acceleration, double deceleration, double velocity)
        {
            var axisNumber = ToAxisNumber(axis);
            var parameters = new GTS.TJogPrm { acc = acceleration, dec = deceleration, smooth = 0 };
            return GTS.GT_PrfJog(axisNumber) == 0 &&
                GTS.GT_SetJogPrm(axisNumber, ref parameters) == 0 &&
                GTS.GT_SetVel(axisNumber, velocity) == 0 &&
                GTS.GT_Update(ToAxisMask(axis)) == 0;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAxis。
        /// 正文中的关键协作调用包括 GT_Stop、ToAxisMask；具体条件和先后次序见方法体。
        /// </summary>
        public bool StopAxis(int axis, bool emergency)
            => GTS.GT_Stop(ToAxisMask(axis), emergency ? 1 : 0) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAll。
        /// 正文中的关键协作调用包括 GT_Stop；具体条件和先后次序见方法体。
        /// </summary>
        public bool StopAll(int axisCount, bool emergency)
        {
            var mask = axisCount >= 64 ? -1L : (1L << axisCount) - 1;
            return GTS.GT_Stop(mask, emergency ? 1 : 0) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetStatus。
        /// 正文中的关键协作调用包括 GT_GetSts、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetStatus(int axis, out int status)
            => GTS.GT_GetSts(ToAxisNumber(axis), out status, 1, IntPtr.Zero) == 0;

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetCommandPosition。
        /// 正文中的关键协作调用包括 GT_GetAxisPrfPos、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetCommandPosition(int axis, out double pulse)
            => GTS.GT_GetAxisPrfPos(ToAxisNumber(axis), out pulse, 1, IntPtr.Zero) == 0;

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetEncoderPosition。
        /// 正文中的关键协作调用包括 GT_GetAxisEncPos、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetEncoderPosition(int axis, out double pulse)
            => GTS.GT_GetAxisEncPos(ToAxisNumber(axis), out pulse, 1, IntPtr.Zero) == 0;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentAsOrigin。
        /// 正文中的关键协作调用包括 GT_ZeroPos、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetCurrentAsOrigin(int axis) => GTS.GT_ZeroPos(ToAxisNumber(axis), 1) == 0;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCommandPosition。
        /// 正文中的关键协作调用包括 GT_SetPrfPos、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetCommandPosition(int axis, int pulse) => GTS.GT_SetPrfPos(ToAxisNumber(axis), pulse) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartHome。
        /// 正文中的关键协作调用包括 ToAxisNumber、GT_ClrSts、GT_GoHome；具体条件和先后次序见方法体。
        /// </summary>
        public bool StartHome(int axis, GoogolHomeProfile profile)
        {
            var axisNumber = ToAxisNumber(axis);
            var parameters = new GTS.THomePrm
            {
                mode = profile.Mode,
                moveDir = profile.Direction,
                indexDir = profile.Direction,
                edge = 0,
                triggerIndex = -1,
                velHigh = profile.HighVelocity,
                velLow = profile.LowVelocity,
                acc = profile.Acceleration,
                dec = profile.Deceleration,
                smoothTime = 0,
                homeOffset = profile.OffsetPulse,
                searchHomeDistance = 0,
                searchIndexDistance = 0,
                escapeStep = 0,
            };
            return GTS.GT_ClrSts(axisNumber, 1) == 0 && GTS.GT_GoHome(axisNumber, ref parameters) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetHomeState。
        /// 正文中的关键协作调用包括 GT_GetHomeStatus、ToAxisNumber；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetHomeState(int axis, out GoogolHomeState state)
        {
            var nativeState = new GTS.THomeStatus();
            var succeeded = GTS.GT_GetHomeStatus(ToAxisNumber(axis), ref nativeState) == 0;
            state = new GoogolHomeState(nativeState.stage, nativeState.error);
            return succeeded;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadInputs。
        /// 正文中的关键协作调用包括 GT_GetDi；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadInputs(out long value)
        {
            value = 0;
            return GTS.GT_GetDi(GTS.MC_GPI, ref value) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadOutputs。
        /// 正文中的关键协作调用包括 GT_GetDo；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadOutputs(out long value)
        {
            value = 0;
            return GTS.GT_GetDo(GTS.MC_GPO, ref value) == 0;
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// 正文中的关键协作调用包括 GT_SetDoBit；具体条件和先后次序见方法体。
        /// </summary>
        public bool WriteOutput(int index, bool state)
            => GTS.GT_SetDoBit(GTS.MC_GPO, (short)index, (short)(state ? 1 : 0)) == 0;

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Close();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToAxisNumber。
        /// </summary>
        private static short ToAxisNumber(int axis) => checked((short)(axis + 1));

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToAxisMask。
        /// </summary>
        private static long ToAxisMask(int axis) => 1L << axis;

        /// <summary>
        /// 执行关闭步骤；对应入口为 CloseFailedOpen。
        /// 正文中的关键协作调用包括 GT_Close；具体条件和先后次序见方法体。
        /// </summary>
        private static void CloseFailedOpen(bool hadOpenChannels)
        {
            if (!hadOpenChannels)
                GTS.GT_Close();
        }
    }
}

