using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Leadshine;
using Xunit;

namespace MotionControl.Tests;

public sealed class LeadshineTransportTests
{
    [Fact]
    public void ConnectMoveAndHome_UseInjectedTransportWithoutNativeSdk()
    {
        var transport = new FakeLeadshineTransport();
        var card = CreateCard(transport);

        try
        {
            Assert.True(card.ConnectResult().IsSuccess);
            Assert.Equal((ushort)7, transport.OpenedCardNumber);
            Assert.Equal(1, transport.RequiredAxes);

            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.SetSpeedResult(0, 12).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 3).IsSuccess);

            Assert.Equal(3000, transport.LastPositionPulse);
            Assert.True(transport.LastMoveWasAbsolute);
            Assert.Equal(12000, transport.LastMaximumVelocity);
            Assert.Equal(0.012, transport.LastAccelerationTime, 3);

            Assert.True(card.StartHomeResult(0).IsSuccess);
            Assert.Equal((ushort)1, transport.LastHomeDirection);
            Assert.True(transport.LastHomeLimitReturnEnabled);
            Assert.Equal(1500, transport.LastHomeOffsetPulse);
        }
        finally
        {
            card.DisConnect();
        }

        Assert.False(transport.IsOpen);
        Assert.Equal(1, transport.CloseCount);
        Assert.True(transport.EmergencyStopCount > 0);
    }

    [Fact]
    public void Connect_TransportFailure_PreservesSpecificResult()
    {
        var transport = new FakeLeadshineTransport { OpenResult = EMotionResult.InvalidParam };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.False(card.IsConnected);
        Assert.Equal(0, transport.CloseCount);
    }

    [Fact]
    public void Connect_ProfileFailure_ClosesTransportAndRollsBack()
    {
        var transport = new FakeLeadshineTransport { ProfileSucceeds = false };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.Fail, result.Code);
        Assert.False(card.IsConnected);
        Assert.False(transport.IsOpen);
        Assert.Equal(1, transport.CloseCount);
    }

    [Fact]
    public void Connect_CardNumberOutsideSdkRange_IsRejectedBeforeOpen()
    {
        var transport = new FakeLeadshineTransport();
        var card = CreateCard(transport);
        card.Param.CardNo = ushort.MaxValue + 1;

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Equal(0, transport.OpenCount);
    }

    private static LeadshineHelper CreateCard(FakeLeadshineTransport transport)
        => new(transport)
        {
            Param = new MotionCardParam
            {
                CardNo = 7,
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

    private sealed class FakeLeadshineTransport : ILeadshineTransport
    {
        public EMotionResult OpenResult { get; set; } = EMotionResult.Success;
        public bool ProfileSucceeds { get; set; } = true;
        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public ushort OpenedCardNumber { get; private set; }
        public int RequiredAxes { get; private set; }
        public int LastPositionPulse { get; private set; }
        public bool LastMoveWasAbsolute { get; private set; }
        public double LastMaximumVelocity { get; private set; }
        public double LastAccelerationTime { get; private set; }
        public ushort LastHomeDirection { get; private set; }
        public bool LastHomeLimitReturnEnabled { get; private set; }
        public int? LastHomeOffsetPulse { get; private set; }
        public int EmergencyStopCount => Volatile.Read(ref _emergencyStopCount);

        private int _emergencyStopCount;

        public EMotionResult Open(ushort cardNumber, int requiredAxes)
        {
            OpenCount++;
            OpenedCardNumber = cardNumber;
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

        public bool SetProfile(int axis, double minimumVelocity, double maximumVelocity,
            double accelerationTime, double decelerationTime, double stopVelocity)
        {
            LastMaximumVelocity = maximumVelocity;
            LastAccelerationTime = accelerationTime;
            return ProfileSucceeds;
        }

        public bool SetServo(int axis, bool enabled) => true;

        public bool MovePosition(int axis, int pulse, bool absolute)
        {
            LastPositionPulse = pulse;
            LastMoveWasAbsolute = absolute;
            return true;
        }

        public bool MoveVelocity(int axis, bool positive) => true;
        public bool Stop(int axis, bool emergency) => true;
        public bool EmergencyStopAll()
        {
            Interlocked.Increment(ref _emergencyStopCount);
            return true;
        }

        public bool ConfigureHome(int axis, ushort direction, double velocity, ushort mode,
            bool enableLimitReturn, int? offsetPulse)
        {
            LastHomeDirection = direction;
            LastHomeLimitReturnEnabled = enableLimitReturn;
            LastHomeOffsetPulse = offsetPulse;
            return true;
        }

        public bool StartHome(int axis) => true;

        public bool TryGetHomeResult(int axis, out ushort state)
        {
            state = 0;
            return true;
        }

        public short CheckDone(int axis) => 1;
        public uint GetAxisIoStatus(int axis) => 0;
        public int GetCommandPosition(int axis) => 0;
        public int GetEncoderPosition(int axis) => 0;
        public bool SetCurrentPosition(int axis, int pulse) => true;
        public short ReadInput(int index) => 0;
        public short ReadOutput(int index) => 0;
        public bool WriteOutput(int index, bool state) => true;
        public void Dispose() => Close();
    }
}

