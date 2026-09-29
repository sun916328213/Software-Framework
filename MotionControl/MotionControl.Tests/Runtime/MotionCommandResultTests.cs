using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Simulation;
using Xunit;

namespace MotionControl.Tests;

public sealed class MotionCommandResultTests
{
    [Fact]
    public void AxisCommand_InvalidAxis_IsDistinguishedFromNotConnected()
    {
        var card = CreateCard();

        var result = card.ServoOnResult(1);

        Assert.Equal(EMotionResult.InvalidAxis, result.Code);
        Assert.Contains("轴索引", result.Message);
    }

    [Fact]
    public void Commands_WhenDisconnected_ReturnNotConnected()
    {
        var card = CreateCard();

        Assert.Equal(EMotionResult.NotConnected, card.ServoOnResult(0).Code);
        Assert.Equal(EMotionResult.NotConnected, card.StopAllResult().Code);
    }

    [Fact]
    public void MotionCommand_InvalidNumber_ReturnsInvalidParam()
    {
        var card = CreateConnectedCard();
        try
        {
            Assert.Equal(EMotionResult.InvalidParam, card.SetSpeedResult(0, double.NaN).Code);
            Assert.Equal(EMotionResult.InvalidParam, card.MoveAbsResult(0, double.PositiveInfinity).Code);
            Assert.Equal(EMotionResult.InvalidParam, card.MoveRelResult(0, double.MaxValue).Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void DriverResultCode_IsPreservedAndLegacyBoolUsesSamePath()
    {
        var card = CreateConnectedCard();
        try
        {
            var disabledMove = card.MoveAbsResult(0, 10);

            Assert.Equal(EMotionResult.Fail, disabledMove.Code);
            Assert.False(card.MoveAbs(0, 10));
            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 10).IsSuccess);
            Assert.True(card.MoveRel(0, -1));
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void InvalidEnumsAndDisabledHoming_ReturnInvalidParam()
    {
        var card = CreateConnectedCard(EHomeMode.None);
        try
        {
            Assert.True(card.ServoOn(0));
            Assert.Equal(EMotionResult.InvalidParam, card.JogStartResult(0, (EDirection)0).Code);
            Assert.Equal(EMotionResult.InvalidParam, card.StopResult(0, (EStopMode)99).Code);
            Assert.Equal(EMotionResult.InvalidParam, card.StopAllResult((EStopMode)99).Code);
            Assert.Equal(EMotionResult.InvalidParam, card.StartHomeResult(0).Code);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void OutputAndDisconnect_ReturnDiagnosticResults()
    {
        var card = CreateConnectedCard();

        Assert.Equal(EMotionResult.InvalidParam, card.SetOutputResult(-1, true).Code);
        Assert.True(card.SetOutputResult(0, true).IsSuccess);
        Assert.True(card.DisconnectResult().IsSuccess);
        Assert.False(card.IsConnected);
        Assert.Equal(EMotionResult.NotConnected, card.SetOutputResult(0, false).Code);
    }

    [Fact]
    public async Task Simulation_SubPulsePerTickSpeed_StillAccumulatesAndArrives()
    {
        var card = CreateConnectedCard();
        try
        {
            Assert.True(card.ServoOn(0));
            Assert.True(card.SetSpeed(0, 0.025)); // 25 pulse/s，单个20ms tick仅0.5 pulse
            Assert.True(card.MoveAbs(0, 0.002)); // 目标2 pulse

            var result = await card.WaitInPositionResultAsync(0, 1000);

            Assert.True(result.IsSuccess);
            Assert.Equal(0.002, card.ReadCommandPosition(0).Value, 6);
        }
        finally
        {
            card.DisConnect();
        }
    }

    [Fact]
    public void Simulation_EmergencyStopBlocksMotionUntilStatusIsCleared()
    {
        var card = CreateConnectedCard();
        try
        {
            Assert.True(card.ServoOn(0));
            Assert.True(card.Stop(0, EStopMode.Emergency));
            Assert.True(card.ReadAxisStatus(0).Value.EmergencyStop);

            Assert.Equal(EMotionResult.Fail, card.MoveAbsResult(0, 1).Code);
            Assert.True(card.ClearAxisStatus(0));
            Assert.True(card.MoveAbsResult(0, 1).IsSuccess);
        }
        finally
        {
            card.DisConnect();
        }
    }

    private static SimulationMotionCard CreateCard(EHomeMode homeMode = EHomeMode.OriginSwitch)
        => new()
        {
            Param = new MotionCardParam
            {
                Axises = [new AxisParam { HomeMode = homeMode }],
            },
        };

    private static SimulationMotionCard CreateConnectedCard(EHomeMode homeMode = EHomeMode.OriginSwitch)
    {
        var card = CreateCard(homeMode);
        Assert.True(card.Connect());
        return card;
    }
}

