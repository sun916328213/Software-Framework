using MotionControl.Definition;

namespace MotionControl.Vendors.Googol
{
    internal readonly record struct GoogolHomeProfile(
        short Mode,
        short Direction,
        double HighVelocity,
        double LowVelocity,
        double Acceleration,
        double Deceleration,
        int OffsetPulse);

    internal readonly record struct GoogolHomeState(short Stage, short Error);

    /// <summary>固高 SDK seam；隐藏1基轴号、位掩码、多步运动提交和进程级库生命周期。</summary>
    internal interface IGoogolTransport : IDisposable
    {
        bool IsOpen { get; }
        EMotionResult Open(short channel, string? configFile, int requiredAxes);
        void Close();
        bool SetServo(int axis, bool enabled);
        bool ClearStatus(int axis);
        bool StartTrapMove(int axis, int targetPulse, double acceleration, double deceleration,
            double startVelocity, double maximumVelocity);
        bool StartJog(int axis, double acceleration, double deceleration, double velocity);
        bool StopAxis(int axis, bool emergency);
        bool StopAll(int axisCount, bool emergency);
        bool TryGetStatus(int axis, out int status);
        bool TryGetCommandPosition(int axis, out double pulse);
        bool TryGetEncoderPosition(int axis, out double pulse);
        bool SetCurrentAsOrigin(int axis);
        bool SetCommandPosition(int axis, int pulse);
        bool StartHome(int axis, GoogolHomeProfile profile);
        bool TryGetHomeState(int axis, out GoogolHomeState state);
        bool TryReadInputs(out long value);
        bool TryReadOutputs(out long value);
        bool WriteOutput(int index, bool state);
    }

    /// <summary>通过 gts.dll 调用真实控制器的生产适配器。</summary>
    internal sealed class GoogolNativeTransport : IGoogolTransport
    {
        private static readonly object LibrarySync = new();
        private static readonly HashSet<short> OpenedChannels = new();
        private short _channel;

        public bool IsOpen { get; private set; }

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

        public bool SetServo(int axis, bool enabled)
            => (enabled ? GTS.GT_AxisOn(ToAxisNumber(axis)) : GTS.GT_AxisOff(ToAxisNumber(axis))) == 0;

        public bool ClearStatus(int axis) => GTS.GT_ClrSts(ToAxisNumber(axis), 1) == 0;

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

        public bool StartJog(int axis, double acceleration, double deceleration, double velocity)
        {
            var axisNumber = ToAxisNumber(axis);
            var parameters = new GTS.TJogPrm { acc = acceleration, dec = deceleration, smooth = 0 };
            return GTS.GT_PrfJog(axisNumber) == 0 &&
                GTS.GT_SetJogPrm(axisNumber, ref parameters) == 0 &&
                GTS.GT_SetVel(axisNumber, velocity) == 0 &&
                GTS.GT_Update(ToAxisMask(axis)) == 0;
        }

        public bool StopAxis(int axis, bool emergency)
            => GTS.GT_Stop(ToAxisMask(axis), emergency ? 1 : 0) == 0;

        public bool StopAll(int axisCount, bool emergency)
        {
            var mask = axisCount >= 64 ? -1L : (1L << axisCount) - 1;
            return GTS.GT_Stop(mask, emergency ? 1 : 0) == 0;
        }

        public bool TryGetStatus(int axis, out int status)
            => GTS.GT_GetSts(ToAxisNumber(axis), out status, 1, IntPtr.Zero) == 0;

        public bool TryGetCommandPosition(int axis, out double pulse)
            => GTS.GT_GetAxisPrfPos(ToAxisNumber(axis), out pulse, 1, IntPtr.Zero) == 0;

        public bool TryGetEncoderPosition(int axis, out double pulse)
            => GTS.GT_GetAxisEncPos(ToAxisNumber(axis), out pulse, 1, IntPtr.Zero) == 0;

        public bool SetCurrentAsOrigin(int axis) => GTS.GT_ZeroPos(ToAxisNumber(axis), 1) == 0;

        public bool SetCommandPosition(int axis, int pulse) => GTS.GT_SetPrfPos(ToAxisNumber(axis), pulse) == 0;

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

        public bool TryGetHomeState(int axis, out GoogolHomeState state)
        {
            var nativeState = new GTS.THomeStatus();
            var succeeded = GTS.GT_GetHomeStatus(ToAxisNumber(axis), ref nativeState) == 0;
            state = new GoogolHomeState(nativeState.stage, nativeState.error);
            return succeeded;
        }

        public bool TryReadInputs(out long value)
        {
            value = 0;
            return GTS.GT_GetDi(GTS.MC_GPI, ref value) == 0;
        }

        public bool TryReadOutputs(out long value)
        {
            value = 0;
            return GTS.GT_GetDo(GTS.MC_GPO, ref value) == 0;
        }

        public bool WriteOutput(int index, bool state)
            => GTS.GT_SetDoBit(GTS.MC_GPO, (short)index, (short)(state ? 1 : 0)) == 0;

        public void Dispose() => Close();

        private static short ToAxisNumber(int axis) => checked((short)(axis + 1));

        private static long ToAxisMask(int axis) => 1L << axis;

        private static void CloseFailedOpen(bool hadOpenChannels)
        {
            if (!hadOpenChannels)
                GTS.GT_Close();
        }
    }
}
