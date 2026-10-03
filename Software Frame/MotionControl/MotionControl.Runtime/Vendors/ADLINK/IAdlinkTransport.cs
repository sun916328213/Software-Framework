// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AdlinkAxisProfile、IAdlinkTransport、AdlinkNativeTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;

namespace MotionControl.Runtime.Vendors.ADLINK
{
    /// <summary>
    /// AdlinkAxisProfile 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
    /// </summary>
    internal readonly record struct AdlinkAxisProfile(
        int MaximumVelocity,
        int Acceleration,
        int Deceleration,
        int HomeMode,
        int HomeDirection,
        int HomeStartVelocity,
        int HomeMaximumVelocity);

    /// <summary>凌华 SDK seam；隐藏板卡位掩码、全局轴ID映射、参数编号和数字IO分组。</summary>
    internal interface IAdlinkTransport : IDisposable
    {
        bool IsOpen { get; }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Open。
        /// </summary>
        EMotionResult Open(int boardNumber, string? configFile, IReadOnlyList<AdlinkAxisProfile> profiles);
        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// </summary>
        void Close();
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetServo。
        /// </summary>
        bool SetServo(int axis, bool enabled);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ResetServo。
        /// </summary>
        bool ResetServo(int axis, bool reenable);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsolute。
        /// </summary>
        bool MoveAbsolute(int axis, int pulse, int maximumSpeed);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveRelative。
        /// </summary>
        bool MoveRelative(int axis, int pulse, int maximumSpeed);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveVelocity。
        /// </summary>
        bool MoveVelocity(int axis, int speed);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAxis。
        /// </summary>
        bool StopAxis(int axis, bool emergency);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAll。
        /// </summary>
        bool StopAll(int axisCount, bool emergency);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartHome。
        /// </summary>
        bool StartHome(int axis);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentPosition。
        /// </summary>
        bool SetCurrentPosition(int axis, int pulse);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetCommandPosition。
        /// </summary>
        bool TryGetCommandPosition(int axis, out int pulse);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetEncoderPosition。
        /// </summary>
        bool TryGetEncoderPosition(int axis, out int pulse);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetMotionStatus。
        /// </summary>
        int GetMotionStatus(int axis);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetMotionIoStatus。
        /// </summary>
        int GetMotionIoStatus(int axis);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadInput。
        /// </summary>
        bool TryReadInput(int index, out bool state);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadOutput。
        /// </summary>
        bool TryReadOutput(int index, out bool state);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// </summary>
        bool WriteOutput(int index, bool state);
    }

    /// <summary>通过 APS168 函数库调用真实板卡的生产适配器。</summary>
    internal sealed class AdlinkNativeTransport : IAdlinkTransport
    {
        private delegate int ReadDigitalGroup(int boardNumber, int group, ref int data);

        private const int DioGroupBits = 32;
        private static readonly object LibrarySync = new();
        private static uint _openBoardMask;
        private int _boardNumber;
        private int[] _axisIds = Array.Empty<int>();

        public bool IsOpen { get; private set; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Open。
        /// 正文中的关键协作调用包括 ThrowIfNull、IsNullOrWhiteSpace、APS_load_param_from_file、APS_initial、APS_get_first_axisId；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public EMotionResult Open(int boardNumber, string? configFile, IReadOnlyList<AdlinkAxisProfile> profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);

            lock (LibrarySync)
            {
                if (IsOpen)
                    return EMotionResult.Success;
                if ((uint)boardNumber > 31 || profiles.Count <= 0)
                    return EMotionResult.InvalidParam;

                var requestedMask = 1u << boardNumber;
                if ((_openBoardMask & requestedMask) != 0)
                    return EMotionResult.InvalidParam;

                if (!string.IsNullOrWhiteSpace(configFile) &&
                    APS168.APS_load_param_from_file(configFile) != APS168.ERR_NoError)
                    return EMotionResult.Fail;

                var hadOpenBoards = _openBoardMask != 0;
                var nativeMask = unchecked((int)requestedMask);
                var initializeResult = APS168.APS_initial(ref nativeMask, 0);
                if (initializeResult != APS168.ERR_NoError &&
                    initializeResult != APS168.ERR_DeviceAlreadyInitialed)
                    return EMotionResult.Fail;

                var startAxisId = 0;
                var totalAxes = 0;
                if (APS168.APS_get_first_axisId(boardNumber, ref startAxisId, ref totalAxes) != APS168.ERR_NoError)
                {
                    CloseFailedOpen(hadOpenBoards);
                    return EMotionResult.Fail;
                }
                if (profiles.Count > totalAxes)
                {
                    CloseFailedOpen(hadOpenBoards);
                    return EMotionResult.InvalidParam;
                }

                var axisIds = Enumerable.Range(startAxisId, profiles.Count).ToArray();
                for (var axis = 0; axis < profiles.Count; axis++)
                {
                    if (ConfigureAxis(axisIds[axis], profiles[axis]))
                        continue;

                    CloseFailedOpen(hadOpenBoards);
                    return EMotionResult.Fail;
                }

                _boardNumber = boardNumber;
                _axisIds = axisIds;
                _openBoardMask |= requestedMask;
                IsOpen = true;
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// 正文中的关键协作调用包括 Empty、APS_close；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Close()
        {
            lock (LibrarySync)
            {
                if (!IsOpen)
                    return;

                IsOpen = false;
                _openBoardMask &= ~(1u << _boardNumber);
                _axisIds = Array.Empty<int>();
                if (_openBoardMask == 0)
                    APS168.APS_close();
            }
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetServo。
        /// 正文中的关键协作调用包括 APS_set_servo_on、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetServo(int axis, bool enabled)
            => APS168.APS_set_servo_on(AxisId(axis), enabled ? 1 : 0) == APS168.ERR_NoError;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ResetServo。
        /// 正文中的关键协作调用包括 SetServo、Sleep；具体条件和先后次序见方法体。
        /// </summary>
        public bool ResetServo(int axis, bool reenable)
        {
            if (!SetServo(axis, false))
                return false;
            Thread.Sleep(100);
            return !reenable || SetServo(axis, true);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsolute。
        /// 正文中的关键协作调用包括 APS_absolute_move、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool MoveAbsolute(int axis, int pulse, int maximumSpeed)
            => APS168.APS_absolute_move(AxisId(axis), pulse, maximumSpeed) == APS168.ERR_NoError;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveRelative。
        /// 正文中的关键协作调用包括 APS_relative_move、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool MoveRelative(int axis, int pulse, int maximumSpeed)
            => APS168.APS_relative_move(AxisId(axis), pulse, maximumSpeed) == APS168.ERR_NoError;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveVelocity。
        /// 正文中的关键协作调用包括 APS_velocity_move、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool MoveVelocity(int axis, int speed)
            => APS168.APS_velocity_move(AxisId(axis), speed) == APS168.ERR_NoError;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAxis。
        /// 正文中的关键协作调用包括 APS_emg_stop、AxisId、APS_stop_move；具体条件和先后次序见方法体。
        /// </summary>
        public bool StopAxis(int axis, bool emergency)
            => (emergency ? APS168.APS_emg_stop(AxisId(axis)) : APS168.APS_stop_move(AxisId(axis))) ==
                APS168.ERR_NoError;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StopAll。
        /// 正文中的关键协作调用包括 StopAxis；具体条件和先后次序见方法体。
        /// </summary>
        public bool StopAll(int axisCount, bool emergency)
        {
            var allSucceeded = true;
            for (var axis = 0; axis < axisCount; axis++)
                allSucceeded &= StopAxis(axis, emergency);
            return allSucceeded;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartHome。
        /// 正文中的关键协作调用包括 APS_home_move、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool StartHome(int axis) => APS168.APS_home_move(AxisId(axis)) == APS168.ERR_NoError;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentPosition。
        /// 正文中的关键协作调用包括 APS_set_command、AxisId、APS_set_position；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetCurrentPosition(int axis, int pulse)
            => APS168.APS_set_command(AxisId(axis), pulse) == APS168.ERR_NoError &&
                APS168.APS_set_position(AxisId(axis), pulse) == APS168.ERR_NoError;

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetCommandPosition。
        /// 正文中的关键协作调用包括 APS_get_command、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetCommandPosition(int axis, out int pulse)
        {
            pulse = 0;
            return APS168.APS_get_command(AxisId(axis), ref pulse) == APS168.ERR_NoError;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetEncoderPosition。
        /// 正文中的关键协作调用包括 APS_get_position、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetEncoderPosition(int axis, out int pulse)
        {
            pulse = 0;
            return APS168.APS_get_position(AxisId(axis), ref pulse) == APS168.ERR_NoError;
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetMotionStatus。
        /// 正文中的关键协作调用包括 APS_motion_status、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public int GetMotionStatus(int axis) => APS168.APS_motion_status(AxisId(axis));

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetMotionIoStatus。
        /// 正文中的关键协作调用包括 APS_motion_io_status、AxisId；具体条件和先后次序见方法体。
        /// </summary>
        public int GetMotionIoStatus(int axis) => APS168.APS_motion_io_status(AxisId(axis));

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadInput。
        /// 正文中的关键协作调用包括 TryReadBit；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadInput(int index, out bool state)
            => TryReadBit(APS168.APS_read_d_input, index, out state);

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadOutput。
        /// 正文中的关键协作调用包括 TryReadBit；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryReadOutput(int index, out bool state)
            => TryReadBit(APS168.APS_read_d_output, index, out state);

        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// 正文中的关键协作调用包括 APS_read_d_output、APS_write_d_output；具体条件和先后次序见方法体。
        /// </summary>
        public bool WriteOutput(int index, bool state)
        {
            var group = index / DioGroupBits;
            var bit = index % DioGroupBits;
            var data = 0;
            if (APS168.APS_read_d_output(_boardNumber, group, ref data) != APS168.ERR_NoError)
                return false;
            data = state ? data | (1 << bit) : data & ~(1 << bit);
            return APS168.APS_write_d_output(_boardNumber, group, data) == APS168.ERR_NoError;
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Close();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AxisId。
        /// </summary>
        private int AxisId(int axis) => _axisIds[axis];

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryReadBit。
        /// 正文中的关键协作调用包括 read；具体条件和先后次序见方法体。
        /// </summary>
        private bool TryReadBit(ReadDigitalGroup read, int index, out bool state)
        {
            var group = index / DioGroupBits;
            var bit = index % DioGroupBits;
            var data = 0;
            var succeeded = read(_boardNumber, group, ref data) == APS168.ERR_NoError;
            state = succeeded && (data & (1 << bit)) != 0;
            return succeeded;
        }

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureAxis。
        /// 正文中的关键协作调用包括 SetAxisParameter；具体条件和先后次序见方法体。
        /// </summary>
        private static bool ConfigureAxis(int axisId, AdlinkAxisProfile profile)
            => SetAxisParameter(axisId, APS168.PRA_MAX_VELOCITY, profile.MaximumVelocity) &&
                SetAxisParameter(axisId, APS168.PRA_ACC, profile.Acceleration) &&
                SetAxisParameter(axisId, APS168.PRA_DEC, profile.Deceleration) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_MODE, profile.HomeMode) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_DIR, profile.HomeDirection) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_ACC, profile.Acceleration) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_VS, profile.HomeStartVelocity) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_VM, profile.HomeMaximumVelocity);

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetAxisParameter。
        /// 正文中的关键协作调用包括 APS_set_axis_param；具体条件和先后次序见方法体。
        /// </summary>
        private static bool SetAxisParameter(int axisId, int parameter, int value)
            => APS168.APS_set_axis_param(axisId, parameter, value) == APS168.ERR_NoError;

        /// <summary>
        /// 执行关闭步骤；对应入口为 CloseFailedOpen。
        /// 正文中的关键协作调用包括 APS_close；具体条件和先后次序见方法体。
        /// </summary>
        private static void CloseFailedOpen(bool hadOpenBoards)
        {
            if (!hadOpenBoards)
                APS168.APS_close();
        }
    }
}

