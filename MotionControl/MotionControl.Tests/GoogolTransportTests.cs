using MotionControl.Definition;
using MotionControl.Vendors.Googol;
using Xunit;

namespace MotionControl.Tests;

public sealed class GoogolTransportTests
{
    [Fact]
    public void ConnectMoveAndHome_UseInjectedTransportWithoutNativeSdk()
    {
        var transport = new FakeGoogolTransport();
        var card = CreateCard(transport);

        try
        {
            Assert.True(card.ConnectResult().IsSuccess);
            Assert.Equal((short)7, transport.OpenedChannel);
            Assert.Equal("machine.cfg", transport.ConfigFile);
            Assert.Equal(1, transport.RequiredAxes);

            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.SetSpeedResult(0, 12).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 3).IsSuccess);

            Assert.Equal(3000, transport.LastTargetPulse);
            Assert.Equal(12000, transport.LastMaximumVelocity);
            Assert.Equal(1, transport.LastAcceleration);
            Assert.Equal(1000, transport.LastStartVelocity);

            Assert.True(card.StartHomeResult(0).IsSuccess);
            Assert.Equal(GTS.HOME_MODE_LIMIT, transport.LastHomeProfile.Mode);
            Assert.Equal((short)EDirection.Positive, transport.LastHomeProfile.Direction);
            Assert.Equal(5000, transport.LastHomeProfile.HighVelocity);
            Assert.Equal(1500, transport.LastHomeProfile.OffsetPulse);
        }
        finally
        {
            card.DisConnect();
        }

        Assert.False(transport.IsOpen);
        Assert.Equal(1, transport.CloseCount);
        Assert.True(transport.StopAllCount > 0);
    }

    [Fact]
    public void Connect_TransportFailure_PreservesSpecificResult()
    {
        var transport = new FakeGoogolTransport { OpenResult = EMotionResult.InvalidParam };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.False(card.IsConnected);
    }

    [Fact]
    public void Move_TransportFailure_IsReported()
    {
        var transport = new FakeGoogolTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        Assert.True(card.ServoOn(0));
        transport.TrapMoveSucceeds = false;

        try
        {
            var result = card.MoveAbsResult(0, 3);

            Assert.Equal(EMotionResult.Fail, result.Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void PositionRead_TransportFailure_IsNotReportedAsZero()
    {
        var transport = new FakeGoogolTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        transport.PositionReadSucceeds = false;

        try
        {
            var result = card.ReadCommandPosition(0);

            Assert.Equal(EMotionResult.Fail, result.Code);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void Connect_ChannelOutsideSdkRange_IsRejectedBeforeOpen()
    {
        var transport = new FakeGoogolTransport();
        var card = CreateCard(transport);
        card.Param.CardNo = short.MaxValue + 1;

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Equal(0, transport.OpenCount);
    }

    private static GoogolHelper CreateCard(FakeGoogolTransport transport)
        => new(transport)
        {
            Param = new MotionCardParam
            {
                CardNo = 7,
                ConfigFile = "machine.cfg",
                Axises =
                [
                    new AxisParam
                    {
                        HomeMode = EHomeMode.LimitSwitch,
                        HomeDirection = EDirection.Positive,
                        HomeOffset = 1.5,
                    },
                ],
            },
        };

    private sealed class FakeGoogolTransport : IGoogolTransport
    {
        public EMotionResult OpenResult { get; set; } = EMotionResult.Success;
        public bool TrapMoveSucceeds { get; set; } = true;
        public bool PositionReadSucceeds { get; set; } = true;
        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int StopAllCount => Volatile.Read(ref _stopAllCount);
        public short OpenedChannel { get; private set; }
        public string? ConfigFile { get; private set; }
        public int RequiredAxes { get; private set; }
        public int LastTargetPulse { get; private set; }
        public double LastMaximumVelocity { get; private set; }
        public double LastAcceleration { get; private set; }
        public double LastStartVelocity { get; private set; }
        public GoogolHomeProfile LastHomeProfile { get; private set; }

        private int _status = GTS.GTS_STS_ZERO_VEL;
        private int _stopAllCount;

        public EMotionResult Open(short channel, string? configFile, int requiredAxes)
        {
            OpenCount++;
            OpenedChannel = channel;
            ConfigFile = configFile;
            RequiredAxes = requiredAxes;
            IsOpen = OpenResult == EMotionResult.Success;
            return OpenResult;
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            CloseCount++;
        }

        public bool SetServo(int axis, bool enabled)
        {
            if (enabled)
                _status |= GTS.GTS_STS_SERVO_ON;
            else
                _status &= ~GTS.GTS_STS_SERVO_ON;
            return true;
        }

        public bool ClearStatus(int axis) => true;

        public bool StartTrapMove(int axis, int targetPulse, double acceleration, double deceleration,
            double startVelocity, double maximumVelocity)
        {
            LastTargetPulse = targetPulse;
            LastMaximumVelocity = maximumVelocity;
            LastAcceleration = acceleration;
            LastStartVelocity = startVelocity;
            return TrapMoveSucceeds;
        }

        public bool StartJog(int axis, double acceleration, double deceleration, double velocity) => true;
        public bool StopAxis(int axis, bool emergency) => true;

        public bool StopAll(int axisCount, bool emergency)
        {
            Interlocked.Increment(ref _stopAllCount);
            return true;
        }

        public bool TryGetStatus(int axis, out int status)
        {
            status = Volatile.Read(ref _status);
            return true;
        }

        public bool TryGetCommandPosition(int axis, out double pulse)
        {
            pulse = 0;
            return PositionReadSucceeds;
        }

        public bool TryGetEncoderPosition(int axis, out double pulse)
        {
            pulse = 0;
            return true;
        }

        public bool SetCurrentAsOrigin(int axis) => true;
        public bool SetCommandPosition(int axis, int pulse) => true;

        public bool StartHome(int axis, GoogolHomeProfile profile)
        {
            LastHomeProfile = profile;
            return true;
        }

        public bool TryGetHomeState(int axis, out GoogolHomeState state)
        {
            state = new GoogolHomeState(0, 0);
            return true;
        }

        public bool TryReadInputs(out long value)
        {
            value = 0;
            return true;
        }

        public bool TryReadOutputs(out long value)
        {
            value = 0;
            return true;
        }

        public bool WriteOutput(int index, bool state) => true;
        public void Dispose() => Close();
    }
}
