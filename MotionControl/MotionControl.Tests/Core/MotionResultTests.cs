using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

public sealed class MotionResultTests
{
    [Fact]
    public void DefaultResult_IsNeverSuccessful()
    {
        MotionResult result = default;
        MotionResult<bool> valueResult = default;

        Assert.False(result.IsSuccess);
        Assert.Equal(EMotionResult.Fail, result.Code);
        Assert.False(valueResult.IsSuccess);
        Assert.Equal(EMotionResult.Fail, valueResult.Code);
    }

    [Fact]
    public void ReadInput_LowLevel_ReturnsSuccessfulFalseInsteadOfFailure()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateConnectedCard(transport);

        try
        {
            var result = card.ReadInput(0);

            Assert.True(result.IsSuccess);
            Assert.False(result.Value);
            Assert.Equal(EMotionResult.Success, result.Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void ReadInput_TransportFailure_IsNotReportedAsLowLevel()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateConnectedCard(transport);
        transport.FailReads = true;

        try
        {
            var result = card.ReadInput(0);

            Assert.False(result.IsSuccess);
            Assert.Equal(EMotionResult.Fail, result.Code);
        }
        finally
        {
            transport.FailReads = false;
            card.DisConnect();
        }
    }

    [Fact]
    public void ReadCommandPosition_InvalidAxis_ReturnsExplicitError()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateConnectedCard(transport);

        try
        {
            var result = card.ReadCommandPosition(1);

            Assert.False(result.IsSuccess);
            Assert.Equal(EMotionResult.InvalidAxis, result.Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void ReadCommandPosition_TransportFailure_IsNotReportedAsZeroPosition()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateConnectedCard(transport);
        transport.FailReads = true;

        try
        {
            var result = card.ReadCommandPosition(0);

            Assert.False(result.IsSuccess);
            Assert.Equal(EMotionResult.Fail, result.Code);
        }
        finally
        {
            transport.FailReads = false;
            card.DisConnect();
        }
    }

    [Fact]
    public async Task WaitInPosition_CancelledToken_ReturnsCancelledResult()
    {
        var transport = new FakeInovanceTransport();
        var card = CreateConnectedCard(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            var result = await card.WaitInPositionResultAsync(0, cancellationToken: cancellation.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal(EMotionResult.Cancelled, result.Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    private static InovanceHelper CreateConnectedCard(FakeInovanceTransport transport)
    {
        var card = new InovanceHelper(transport)
        {
            Param = new InovanceParam
            {
                IpAddress = "127.0.0.1",
                Axises = [new AxisParam()],
            },
        };

        Assert.True(card.Connect());
        return card;
    }

    private sealed class FakeInovanceTransport : IInovanceTransport
    {
        public bool FailReads { get; set; }
        public bool IsConnected { get; private set; }

        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
        {
            IsConnected = true;
            return true;
        }

        public void Disconnect() => IsConnected = false;

        public bool TryReadStatus(int axis, out ushort status)
        {
            status = 0;
            return !FailReads;
        }

        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask) => true;
        public bool WriteTarget(int axis, int position, float velocity) => true;

        public bool TryReadCurrentPosition(int axis, out int position)
        {
            position = 0;
            return !FailReads;
        }

        public bool SetCurrentPosition(int axis, int position) => true;

        public bool TryReadInput(int index, out bool state)
        {
            state = false;
            return !FailReads;
        }

        public bool TryReadOutput(int index, out bool state)
        {
            state = false;
            return !FailReads;
        }

        public bool WriteOutput(int index, bool state) => true;
        public void Dispose() => Disconnect();
    }
}

