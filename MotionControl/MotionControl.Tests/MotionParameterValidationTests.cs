using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Inovance;
using MotionControl.Runtime.Vendors.Simulation;
using MotionControl.Runtime.Vendors.ZMotion;
using Xunit;

namespace MotionControl.Tests;

public sealed class MotionParameterValidationTests
{
    [Fact]
    public void ConnectResult_InvalidCommonAxisParameters_DoNotEnterDriver()
    {
        var card = new SimulationMotionCard
        {
            Param = new MotionCardParam
            {
                Axises =
                [
                    new AxisParam
                    {
                        PulseEquivalent = 0,
                    },
                ],
            },
        };

        var result = card.ConnectResult();

        Assert.False(result.IsSuccess);
        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Contains("脉冲当量", result.Message);
        Assert.False(card.IsConnected);
    }

    [Fact]
    public void ConnectResult_InvalidSoftLimits_ReturnsDiagnosticMessage()
    {
        var card = new SimulationMotionCard
        {
            Param = new MotionCardParam
            {
                Axises =
                [
                    new AxisParam
                    {
                        EnableSoftLimit = true,
                        SoftLimitNegative = 10,
                        SoftLimitPositive = -10,
                    },
                ],
            },
        };

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Contains("软限位", result.Message);
    }

    [Fact]
    public void ConnectResult_ValidSimulationConfiguration_ConnectsSuccessfully()
    {
        var card = new SimulationMotionCard
        {
            Param = new MotionCardParam { Axises = [new AxisParam()] },
        };

        try
        {
            var result = card.ConnectResult();

            Assert.True(result.IsSuccess);
            Assert.True(card.IsConnected);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void MotionCommands_NonFiniteOrOverflowingValues_ReturnFalse()
    {
        var card = new SimulationMotionCard
        {
            Param = new MotionCardParam { Axises = [new AxisParam()] },
        };
        Assert.True(card.Connect());

        try
        {
            Assert.False(card.SetSpeed(0, double.NaN));
            Assert.False(card.MoveAbs(0, double.PositiveInfinity));
            Assert.False(card.MoveRel(0, double.MaxValue));
            Assert.False(card.MoveAbs(1, 10));
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void ConnectResult_PulseConversionOverflow_IsRejected()
    {
        var card = new SimulationMotionCard
        {
            Param = new MotionCardParam
            {
                Axises = [new AxisParam { PulseEquivalent = double.MaxValue }],
            },
        };

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Contains("Int64", result.Message);
    }

    [Fact]
    public void ConnectedCard_UsesStableSnapshotUntilNextConnection()
    {
        var axis = new AxisParam();
        var parameter = new MotionCardParam
        {
            Name = "Active",
            Axises = [axis],
        };
        var card = new SimulationMotionCard { Param = parameter };
        Assert.True(card.Connect());

        axis.PulseEquivalent = 0;
        parameter.Name = "Staged";
        parameter.Axises.Add(new AxisParam());

        Assert.Same(parameter, card.Param);
        Assert.Equal("Active", card.Name);
        Assert.Equal(1, card.AxisCount);
        Assert.True(card.ServoOn(0));
        Assert.True(card.MoveAbs(0, 1));

        Assert.True(card.DisConnect());
        Assert.Equal("Staged", card.Name);
        Assert.Equal(2, card.AxisCount);
        Assert.Equal(EMotionResult.InvalidParam, card.ConnectResult().Code);
    }

    [Fact]
    public void ParameterSnapshot_PreservesDerivedTypeAndClonesAxes()
    {
        var axis = new AxisParam { PulseEquivalent = 2000 };
        var parameter = new InovanceParam
        {
            RegisterBase = 500,
            Axises = [axis],
        };

        var snapshot = Assert.IsType<InovanceParam>(parameter.Snapshot());
        axis.PulseEquivalent = 1;
        parameter.Axises.Add(new AxisParam());

        Assert.Equal((ushort)500, snapshot.RegisterBase);
        Assert.Single(snapshot.Axises);
        Assert.Equal(2000, snapshot.Axises[0].PulseEquivalent);
        Assert.NotSame(axis, snapshot.Axises[0]);
    }

    [Fact]
    public void ZMotion_MissingIpAddress_IsRejectedBeforeNativeSdkCall()
    {
        var card = new ZMotionHelper
        {
            Param = new MotionCardParam { Axises = [new AxisParam()] },
        };

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Contains("IP 地址", result.Message);
    }

    [Fact]
    public void Inovance_RegisterMapOverflow_IsRejectedBeforeTransportCall()
    {
        var card = new InovanceHelper(new TrackingTransport())
        {
            Param = new InovanceParam
            {
                IpAddress = "127.0.0.1",
                RegisterBase = 65530,
                RegisterStride = 20,
                Axises = [new AxisParam(), new AxisParam()],
            },
        };

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.Contains("寄存器映射", result.Message);
    }

    private sealed class TrackingTransport : IInovanceTransport
    {
        public bool IsConnected => false;
        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
            => throw new InvalidOperationException("参数无效时不应访问传输层。");
        public void Disconnect() { }
        public bool TryReadStatus(int axis, out ushort status) => throw new NotSupportedException();
        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask) => throw new NotSupportedException();
        public bool WriteTarget(int axis, int position, float velocity) => throw new NotSupportedException();
        public bool TryReadCurrentPosition(int axis, out int position) => throw new NotSupportedException();
        public bool SetCurrentPosition(int axis, int position) => throw new NotSupportedException();
        public bool TryReadInput(int index, out bool state) => throw new NotSupportedException();
        public bool TryReadOutput(int index, out bool state) => throw new NotSupportedException();
        public bool WriteOutput(int index, bool state) => throw new NotSupportedException();
        public void Dispose() { }
    }
}

