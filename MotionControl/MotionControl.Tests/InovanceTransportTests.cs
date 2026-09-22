using MotionControl.Definition;
using MotionControl.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

public sealed class InovanceTransportTests
{
    [Fact]
    public void Connect_TransportFailure_IsReported()
    {
        var transport = new FakeInovanceTransport { ConnectSucceeds = false };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.Fail, result.Code);
        Assert.False(card.IsConnected);
    }

    [Fact]
    public void ConnectAndMove_UseDomainTransportWithoutRegisterKnowledge()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateCard(transport);

        try
        {
            Assert.True(card.ConnectResult().IsSuccess);
            Assert.Equal("127.0.0.1", transport.ConnectedIpAddress);
            Assert.Equal(502, transport.ConnectedPort);
            Assert.Equal((byte)2, transport.Parameter?.SlaveId);
            Assert.Equal(1, transport.RequiredAxes);

            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.SetSpeedResult(0, 12).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 3).IsSuccess);

            Assert.Equal(3000, transport.LastTargetPosition);
            Assert.Equal(12f, transport.LastTargetVelocity);
            Assert.True(transport.ControlWordUpdateCount >= 2);
        }
        finally
        {
            card.DisConnect();
        }

        Assert.False(transport.IsConnected);
    }

    [Fact]
    public void Move_TargetWriteFailure_DoesNotSendStartCommand()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        Assert.True(card.ServoOn(0));
        var updatesBeforeMove = transport.ControlWordUpdateCount;
        transport.TargetWriteSucceeds = false;

        try
        {
            var result = card.MoveAbsResult(0, 3);

            Assert.Equal(EMotionResult.Fail, result.Code);
            Assert.Equal(updatesBeforeMove, transport.ControlWordUpdateCount);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void StopAll_ControlWriteFailure_IsReported()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        transport.ControlWriteSucceeds = false;

        try
        {
            var result = card.StopAllResult();

            Assert.Equal(EMotionResult.Fail, result.Code);
        }
        finally
        {
            transport.ControlWriteSucceeds = true;
            card.DisConnect();
        }
    }

    [Fact]
    public void SetCurrentAsOrigin_WriteFailure_IsReported()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateCard(transport, EHomeMode.SetCurrentAsOrigin);
        Assert.True(card.Connect());
        transport.CurrentPositionWriteSucceeds = false;

        try
        {
            var result = card.StartHomeResult(0);

            Assert.Equal(EMotionResult.Fail, result.Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void IoAddressOverflow_IsRejectedBeforeTransportCall()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());

        try
        {
            var result = card.SetOutputResult(ushort.MaxValue, true);

            Assert.Equal(EMotionResult.InvalidParam, result.Code);
            Assert.Equal(0, transport.OutputWriteCount);
        }
        finally
        {
            card.DisConnect();
        }
    }

    private static InovanceHelper CreateCard(FakeInovanceTransport transport,
        EHomeMode homeMode = EHomeMode.OriginSwitch)
        => new(transport)
        {
            Param = new InovanceParam
            {
                IpAddress = "127.0.0.1",
                TcpPort = 502,
                SlaveId = 2,
                RegisterBase = 100,
                Axises = [new AxisParam { HomeMode = homeMode }],
            },
        };

    private sealed class FakeInovanceTransport : IInovanceTransport
    {
        public bool ConnectSucceeds { get; set; } = true;
        public bool TargetWriteSucceeds { get; set; } = true;
        public bool ControlWriteSucceeds { get; set; } = true;
        public bool CurrentPositionWriteSucceeds { get; set; } = true;
        public bool IsConnected { get; private set; }
        public string? ConnectedIpAddress { get; private set; }
        public int ConnectedPort { get; private set; }
        public InovanceParam? Parameter { get; private set; }
        public int RequiredAxes { get; private set; }
        public int LastTargetPosition { get; private set; }
        public float LastTargetVelocity { get; private set; }
        public int ControlWordUpdateCount => Volatile.Read(ref _controlWordUpdateCount);
        public int OutputWriteCount { get; private set; }

        private int _controlWordUpdateCount;

        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
        {
            ConnectedIpAddress = ipAddress;
            ConnectedPort = port;
            Parameter = parameter;
            RequiredAxes = requiredAxes;
            IsConnected = ConnectSucceeds;
            return ConnectSucceeds;
        }

        public void Disconnect() => IsConnected = false;

        public bool TryReadStatus(int axis, out ushort status)
        {
            status = 1;
            return true;
        }

        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask)
        {
            Interlocked.Increment(ref _controlWordUpdateCount);
            return ControlWriteSucceeds;
        }

        public bool WriteTarget(int axis, int position, float velocity)
        {
            LastTargetPosition = position;
            LastTargetVelocity = velocity;
            return TargetWriteSucceeds;
        }

        public bool TryReadCurrentPosition(int axis, out int position)
        {
            position = 0;
            return true;
        }

        public bool SetCurrentPosition(int axis, int position) => CurrentPositionWriteSucceeds;

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

        public bool WriteOutput(int index, bool state)
        {
            OutputWriteCount++;
            return true;
        }

        public void Dispose() => Disconnect();
    }
}
