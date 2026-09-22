using MotionControl.Definition;
using MotionControl.Vendors.ADLINK;
using Xunit;

namespace MotionControl.Tests;

public sealed class AdlinkTransportTests
{
    [Fact]
    public void ConnectMoveAndHome_UseInjectedTransportWithoutNativeSdk()
    {
        var transport = new FakeAdlinkTransport();
        var card = CreateCard(transport);

        try
        {
            Assert.True(card.ConnectResult().IsSuccess);
            Assert.Equal(7, transport.OpenedBoardNumber);
            Assert.Equal("motion.xml", transport.ConfigFile);
            var profile = Assert.Single(transport.Profiles);
            Assert.Equal(100000, profile.MaximumVelocity);
            Assert.Equal(1_000_000, profile.Acceleration);
            Assert.Equal(3, profile.HomeMode);
            Assert.Equal(0, profile.HomeDirection);

            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.SetSpeedResult(0, 12).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 3).IsSuccess);

            Assert.Equal(3000, transport.LastTargetPulse);
            Assert.Equal(12000, transport.LastMaximumSpeed);
            Assert.True(card.StartHomeResult(0).IsSuccess);
            Assert.Equal(1, transport.StartHomeCount);
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
        var transport = new FakeAdlinkTransport { OpenResult = EMotionResult.InvalidParam };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.False(card.IsConnected);
    }

    [Fact]
    public void Move_WhenServoIsOff_ReturnsFailRatherThanNotConnected()
    {
        var transport = new FakeAdlinkTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());

        try
        {
            var result = card.MoveAbsResult(0, 3);

            Assert.Equal(EMotionResult.Fail, result.Code);
            Assert.True(card.IsConnected);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void Move_TransportFailure_IsReported()
    {
        var transport = new FakeAdlinkTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        Assert.True(card.ServoOn(0));
        transport.MoveSucceeds = false;

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
        var transport = new FakeAdlinkTransport();
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
    public void Connect_BoardNumberOutsideMaskRange_IsRejectedBeforeOpen()
    {
        var transport = new FakeAdlinkTransport();
        var card = CreateCard(transport);
        card.Param.CardNo = 32;

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Equal(0, transport.OpenCount);
    }

    private static AdlinkHelper CreateCard(FakeAdlinkTransport transport)
        => new(transport)
        {
            Param = new MotionCardParam
            {
                CardNo = 7,
                ConfigFile = "motion.xml",
                Axises =
                [
                    new AxisParam
                    {
                        HomeMode = EHomeMode.LimitSwitch,
                        HomeDirection = EDirection.Positive,
                    },
                ],
            },
        };

    private sealed class FakeAdlinkTransport : IAdlinkTransport
    {
        public EMotionResult OpenResult { get; set; } = EMotionResult.Success;
        public bool MoveSucceeds { get; set; } = true;
        public bool PositionReadSucceeds { get; set; } = true;
        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int StopAllCount => Volatile.Read(ref _stopAllCount);
        public int OpenedBoardNumber { get; private set; }
        public string? ConfigFile { get; private set; }
        public IReadOnlyList<AdlinkAxisProfile> Profiles { get; private set; } = [];
        public int LastTargetPulse { get; private set; }
        public int LastMaximumSpeed { get; private set; }
        public int StartHomeCount { get; private set; }

        private int _stopAllCount;

        public EMotionResult Open(int boardNumber, string? configFile,
            IReadOnlyList<AdlinkAxisProfile> profiles)
        {
            OpenCount++;
            OpenedBoardNumber = boardNumber;
            ConfigFile = configFile;
            Profiles = profiles.ToArray();
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

        public bool SetServo(int axis, bool enabled) => true;
        public bool ResetServo(int axis, bool reenable) => true;

        public bool MoveAbsolute(int axis, int pulse, int maximumSpeed)
        {
            LastTargetPulse = pulse;
            LastMaximumSpeed = maximumSpeed;
            return MoveSucceeds;
        }

        public bool MoveRelative(int axis, int pulse, int maximumSpeed) => MoveSucceeds;
        public bool MoveVelocity(int axis, int speed) => MoveSucceeds;
        public bool StopAxis(int axis, bool emergency) => true;

        public bool StopAll(int axisCount, bool emergency)
        {
            Interlocked.Increment(ref _stopAllCount);
            return true;
        }

        public bool StartHome(int axis)
        {
            StartHomeCount++;
            return true;
        }

        public bool SetCurrentPosition(int axis, int pulse) => true;

        public bool TryGetCommandPosition(int axis, out int pulse)
        {
            pulse = 0;
            return PositionReadSucceeds;
        }

        public bool TryGetEncoderPosition(int axis, out int pulse)
        {
            pulse = 0;
            return true;
        }

        public int GetMotionStatus(int axis) => 0;
        public int GetMotionIoStatus(int axis) => 0;

        public bool TryReadInput(int index, out bool state)
        {
            state = false;
            return true;
        }

        public bool TryReadOutput(int index, out bool state)
        {
            state = false;
            return true;
        }

        public bool WriteOutput(int index, bool state) => true;
        public void Dispose() => Close();
    }
}
