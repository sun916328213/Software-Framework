using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.ZMotion;
using Xunit;

namespace MotionControl.Tests;

public sealed class ZMotionTransportTests
{
    [Fact]
    public void ConnectAndMove_UseInjectedTransportWithoutNativeSdk()
    {
        var transport = new FakeZMotionTransport();
        var card = CreateCard(transport);

        try
        {
            Assert.True(card.ConnectResult().IsSuccess);
            Assert.Equal("127.0.0.1", transport.ConnectedIpAddress);
            Assert.Equal((uint)502, transport.ConnectedPort);
            Assert.Equal([0], transport.ConfiguredAxes);

            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.SetSpeedResult(0, 12).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 3).IsSuccess);

            Assert.Equal(3000, transport.LastAbsolutePulse);
            Assert.Equal(12000f, transport.LastSpeed);
            Assert.Equal(1_000_000f, transport.LastAcceleration);
            Assert.Equal(1_000_000f, transport.LastDeceleration);
        }
        finally
        {
            card.DisConnect();
        }

        Assert.False(transport.IsConnected);
        Assert.True(transport.CancelCount > 0);
    }

    [Fact]
    public void Connect_TransportFailure_ReturnsFail()
    {
        var transport = new FakeZMotionTransport { ConnectSucceeds = false };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.Fail, result.Code);
        Assert.False(card.IsConnected);
    }

    [Fact]
    public void PositionRead_TransportFailure_IsNotReportedAsZero()
    {
        var transport = new FakeZMotionTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        transport.CommandPositionReadSucceeds = false;

        try
        {
            var result = card.ReadCommandPosition(0);

            Assert.Equal(EMotionResult.Fail, result.Code);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            transport.CommandPositionReadSucceeds = true;
            card.DisConnect();
        }
    }

    private static ZMotionHelper CreateCard(FakeZMotionTransport transport)
        => new(transport)
        {
            Param = new MotionCardParam
            {
                IpAddress = "127.0.0.1",
                TcpPort = 502,
                Axises = [new AxisParam()],
            },
        };

    private sealed class FakeZMotionTransport : IZMotionTransport
    {
        public bool ConnectSucceeds { get; set; } = true;
        public bool CommandPositionReadSucceeds { get; set; } = true;
        public bool IsConnected { get; private set; }
        public string? ConnectedIpAddress { get; private set; }
        public uint ConnectedPort { get; private set; }
        public List<int> ConfiguredAxes { get; } = new();
        public long LastAbsolutePulse { get; private set; }
        public float LastSpeed { get; private set; }
        public float LastAcceleration { get; private set; }
        public float LastDeceleration { get; private set; }
        public int CancelCount => Volatile.Read(ref _cancelCount);

        public bool Connect(string ipAddress, uint port)
        {
            ConnectedIpAddress = ipAddress;
            ConnectedPort = port;
            IsConnected = ConnectSucceeds;
            return ConnectSucceeds;
        }

        public void Disconnect() => IsConnected = false;

        public bool ConfigureAxis(int axis)
        {
            ConfiguredAxes.Add(axis);
            return true;
        }

        public bool SetMotionProfile(int axis, float speed, float acceleration, float deceleration)
        {
            LastSpeed = speed;
            LastAcceleration = acceleration;
            LastDeceleration = deceleration;
            return true;
        }

        public bool MoveAbsolute(int axis, long pulse)
        {
            LastAbsolutePulse = pulse;
            return true;
        }

        public bool MoveRelative(int axis, long pulse) => true;
        public bool Jog(int axis, int direction) => true;

        public bool Cancel(int axis, int mode)
        {
            Interlocked.Increment(ref _cancelCount);
            return true;
        }

        private int _cancelCount;

        public bool Datum(int axis, int mode) => true;
        public bool SetCommandPosition(int axis, float pulse) => true;

        public bool TryGetCommandPosition(int axis, out float pulse)
        {
            pulse = 0;
            return CommandPositionReadSucceeds;
        }

        public bool TryGetFeedbackPosition(int axis, out float pulse)
        {
            pulse = 0;
            return true;
        }

        public bool TryGetAxisStatus(int axis, out int status)
        {
            status = 0;
            return true;
        }

        public bool TryGetIdle(int axis, out int idle)
        {
            idle = 0;
            return true;
        }

        public bool TryGetInput(int index, out uint value)
        {
            value = 0;
            return true;
        }

        public bool TryGetOutput(int index, out uint value)
        {
            value = 0;
            return true;
        }

        public bool SetOutput(int index, uint value) => true;
        public void Dispose() => Disconnect();
    }
}

