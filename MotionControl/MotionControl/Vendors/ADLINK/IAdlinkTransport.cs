using MotionControl.Definition;

namespace MotionControl.Vendors.ADLINK
{
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
        EMotionResult Open(int boardNumber, string? configFile, IReadOnlyList<AdlinkAxisProfile> profiles);
        void Close();
        bool SetServo(int axis, bool enabled);
        bool ResetServo(int axis, bool reenable);
        bool MoveAbsolute(int axis, int pulse, int maximumSpeed);
        bool MoveRelative(int axis, int pulse, int maximumSpeed);
        bool MoveVelocity(int axis, int speed);
        bool StopAxis(int axis, bool emergency);
        bool StopAll(int axisCount, bool emergency);
        bool StartHome(int axis);
        bool SetCurrentPosition(int axis, int pulse);
        bool TryGetCommandPosition(int axis, out int pulse);
        bool TryGetEncoderPosition(int axis, out int pulse);
        int GetMotionStatus(int axis);
        int GetMotionIoStatus(int axis);
        bool TryReadInput(int index, out bool state);
        bool TryReadOutput(int index, out bool state);
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

        public bool SetServo(int axis, bool enabled)
            => APS168.APS_set_servo_on(AxisId(axis), enabled ? 1 : 0) == APS168.ERR_NoError;

        public bool ResetServo(int axis, bool reenable)
        {
            if (!SetServo(axis, false))
                return false;
            Thread.Sleep(100);
            return !reenable || SetServo(axis, true);
        }

        public bool MoveAbsolute(int axis, int pulse, int maximumSpeed)
            => APS168.APS_absolute_move(AxisId(axis), pulse, maximumSpeed) == APS168.ERR_NoError;

        public bool MoveRelative(int axis, int pulse, int maximumSpeed)
            => APS168.APS_relative_move(AxisId(axis), pulse, maximumSpeed) == APS168.ERR_NoError;

        public bool MoveVelocity(int axis, int speed)
            => APS168.APS_velocity_move(AxisId(axis), speed) == APS168.ERR_NoError;

        public bool StopAxis(int axis, bool emergency)
            => (emergency ? APS168.APS_emg_stop(AxisId(axis)) : APS168.APS_stop_move(AxisId(axis))) ==
                APS168.ERR_NoError;

        public bool StopAll(int axisCount, bool emergency)
        {
            var allSucceeded = true;
            for (var axis = 0; axis < axisCount; axis++)
                allSucceeded &= StopAxis(axis, emergency);
            return allSucceeded;
        }

        public bool StartHome(int axis) => APS168.APS_home_move(AxisId(axis)) == APS168.ERR_NoError;

        public bool SetCurrentPosition(int axis, int pulse)
            => APS168.APS_set_command(AxisId(axis), pulse) == APS168.ERR_NoError &&
                APS168.APS_set_position(AxisId(axis), pulse) == APS168.ERR_NoError;

        public bool TryGetCommandPosition(int axis, out int pulse)
        {
            pulse = 0;
            return APS168.APS_get_command(AxisId(axis), ref pulse) == APS168.ERR_NoError;
        }

        public bool TryGetEncoderPosition(int axis, out int pulse)
        {
            pulse = 0;
            return APS168.APS_get_position(AxisId(axis), ref pulse) == APS168.ERR_NoError;
        }

        public int GetMotionStatus(int axis) => APS168.APS_motion_status(AxisId(axis));

        public int GetMotionIoStatus(int axis) => APS168.APS_motion_io_status(AxisId(axis));

        public bool TryReadInput(int index, out bool state)
            => TryReadBit(APS168.APS_read_d_input, index, out state);

        public bool TryReadOutput(int index, out bool state)
            => TryReadBit(APS168.APS_read_d_output, index, out state);

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

        public void Dispose() => Close();

        private int AxisId(int axis) => _axisIds[axis];

        private bool TryReadBit(ReadDigitalGroup read, int index, out bool state)
        {
            var group = index / DioGroupBits;
            var bit = index % DioGroupBits;
            var data = 0;
            var succeeded = read(_boardNumber, group, ref data) == APS168.ERR_NoError;
            state = succeeded && (data & (1 << bit)) != 0;
            return succeeded;
        }

        private static bool ConfigureAxis(int axisId, AdlinkAxisProfile profile)
            => SetAxisParameter(axisId, APS168.PRA_MAX_VELOCITY, profile.MaximumVelocity) &&
                SetAxisParameter(axisId, APS168.PRA_ACC, profile.Acceleration) &&
                SetAxisParameter(axisId, APS168.PRA_DEC, profile.Deceleration) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_MODE, profile.HomeMode) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_DIR, profile.HomeDirection) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_ACC, profile.Acceleration) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_VS, profile.HomeStartVelocity) &&
                SetAxisParameter(axisId, APS168.PRA_HOME_VM, profile.HomeMaximumVelocity);

        private static bool SetAxisParameter(int axisId, int parameter, int value)
            => APS168.APS_set_axis_param(axisId, parameter, value) == APS168.ERR_NoError;

        private static void CloseFailedOpen(bool hadOpenBoards)
        {
            if (!hadOpenBoards)
                APS168.APS_close();
        }
    }
}
