using MotionControl.Definition;

namespace MotionControl.Vendors.Leadshine
{
    /// <summary>雷赛 LTDMC 原生 SDK seam；实现负责进程级库生命周期和当前卡号。</summary>
    internal interface ILeadshineTransport : IDisposable
    {
        bool IsOpen { get; }
        EMotionResult Open(ushort cardNumber, int requiredAxes);
        void Close();
        bool SetProfile(int axis, double minimumVelocity, double maximumVelocity,
            double accelerationTime, double decelerationTime, double stopVelocity);
        bool SetServo(int axis, bool enabled);
        bool MovePosition(int axis, int pulse, bool absolute);
        bool MoveVelocity(int axis, bool positive);
        bool Stop(int axis, bool emergency);
        bool EmergencyStopAll();
        bool ConfigureHome(int axis, ushort direction, double velocity, ushort mode,
            bool enableLimitReturn, int? offsetPulse);
        bool StartHome(int axis);
        bool TryGetHomeResult(int axis, out ushort state);
        short CheckDone(int axis);
        uint GetAxisIoStatus(int axis);
        int GetCommandPosition(int axis);
        int GetEncoderPosition(int axis);
        bool SetCurrentPosition(int axis, int pulse);
        short ReadInput(int index);
        short ReadOutput(int index);
        bool WriteOutput(int index, bool state);
    }

    /// <summary>通过 LTDMC.dll 调用真实板卡的生产适配器。</summary>
    internal sealed class LeadshineNativeTransport : ILeadshineTransport
    {
        private static readonly object LibrarySync = new();
        private static int _openCount;
        private ushort _cardNumber;

        public bool IsOpen { get; private set; }

        public EMotionResult Open(ushort cardNumber, int requiredAxes)
        {
            lock (LibrarySync)
            {
                if (IsOpen)
                    return EMotionResult.Success;

                var initializedHere = false;
                if (_openCount == 0)
                {
                    if (LTDMC.dmc_board_init() <= 0)
                        return EMotionResult.Fail;
                    initializedHere = true;
                }

                ushort cardCount = 0;
                var typeList = new uint[16];
                var idList = new ushort[16];
                if (LTDMC.dmc_get_CardInfList(ref cardCount, typeList, idList) != 0 ||
                    !idList.Take(cardCount).Contains(cardNumber))
                {
                    CloseFailedOpen(initializedHere);
                    return EMotionResult.Fail;
                }

                uint totalAxes = 0;
                if (LTDMC.dmc_get_total_axes(cardNumber, ref totalAxes) != 0 ||
                    requiredAxes <= 0 || requiredAxes > totalAxes)
                {
                    CloseFailedOpen(initializedHere);
                    return EMotionResult.InvalidParam;
                }

                _cardNumber = cardNumber;
                IsOpen = true;
                _openCount++;
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
                if (_openCount > 0)
                    _openCount--;
                if (_openCount == 0)
                    LTDMC.dmc_board_close();
            }
        }

        public bool SetProfile(int axis, double minimumVelocity, double maximumVelocity,
            double accelerationTime, double decelerationTime, double stopVelocity)
            => LTDMC.dmc_set_profile(_cardNumber, (ushort)axis, minimumVelocity, maximumVelocity,
                accelerationTime, decelerationTime, stopVelocity) == 0;

        public bool SetServo(int axis, bool enabled)
            => LTDMC.dmc_write_sevon_pin(_cardNumber, (ushort)axis,
                enabled ? (ushort)0 : (ushort)1) == 0;

        public bool MovePosition(int axis, int pulse, bool absolute)
            => LTDMC.dmc_pmove(_cardNumber, (ushort)axis, pulse, absolute ? (ushort)1 : (ushort)0) == 0;

        public bool MoveVelocity(int axis, bool positive)
            => LTDMC.dmc_vmove(_cardNumber, (ushort)axis, positive ? (ushort)1 : (ushort)0) == 0;

        public bool Stop(int axis, bool emergency)
            => LTDMC.dmc_stop(_cardNumber, (ushort)axis, emergency ? (ushort)1 : (ushort)0) == 0;

        public bool EmergencyStopAll() => LTDMC.dmc_emg_stop(_cardNumber) == 0;

        public bool ConfigureHome(int axis, ushort direction, double velocity, ushort mode,
            bool enableLimitReturn, int? offsetPulse)
        {
            if (LTDMC.dmc_set_homemode(_cardNumber, (ushort)axis, direction, velocity, mode, 0) != 0)
                return false;
            if (LTDMC.dmc_set_home_el_return(_cardNumber, (ushort)axis,
                    enableLimitReturn ? (ushort)1 : (ushort)0) != 0)
                return false;
            return !offsetPulse.HasValue ||
                LTDMC.dmc_set_home_position(_cardNumber, (ushort)axis, 1, offsetPulse.Value) == 0;
        }

        public bool StartHome(int axis) => LTDMC.dmc_home_move(_cardNumber, (ushort)axis) == 0;

        public bool TryGetHomeResult(int axis, out ushort state)
        {
            state = 0;
            return LTDMC.dmc_get_home_result(_cardNumber, (ushort)axis, ref state) == 0;
        }

        public short CheckDone(int axis) => LTDMC.dmc_check_done(_cardNumber, (ushort)axis);

        public uint GetAxisIoStatus(int axis) => LTDMC.dmc_axis_io_status(_cardNumber, (ushort)axis);

        public int GetCommandPosition(int axis) => LTDMC.dmc_get_position(_cardNumber, (ushort)axis);

        public int GetEncoderPosition(int axis) => LTDMC.dmc_get_encoder(_cardNumber, (ushort)axis);

        public bool SetCurrentPosition(int axis, int pulse)
            => LTDMC.dmc_set_position(_cardNumber, (ushort)axis, pulse) == 0 &&
                LTDMC.dmc_set_encoder(_cardNumber, (ushort)axis, pulse) == 0;

        public short ReadInput(int index) => LTDMC.dmc_read_inbit(_cardNumber, (ushort)index);

        public short ReadOutput(int index) => LTDMC.dmc_read_outbit(_cardNumber, (ushort)index);

        public bool WriteOutput(int index, bool state)
            => LTDMC.dmc_write_outbit(_cardNumber, (ushort)index, (ushort)(state ? 1 : 0)) == 0;

        public void Dispose() => Close();

        private static void CloseFailedOpen(bool initializedHere)
        {
            if (initializedHere && _openCount == 0)
                LTDMC.dmc_board_close();
        }
    }
}
