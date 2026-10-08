// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MotionCommandResultTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.Simulation、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Simulation;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// MotionCommandResultTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MotionCommandResultTests
{
    /// <summary>
    /// 测试相关入口 AxisCommand_InvalidAxis_IsDistinguishedFromNotConnected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ServoOnResult、Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AxisCommand_InvalidAxis_IsDistinguishedFromNotConnected()
    {
        var card = CreateCard();

        var result = card.ServoOnResult(1);

        Assert.Equal(EMotionResult.InvalidAxis, result.Code);
        Assert.Contains("轴索引", result.Message);
    }

    /// <summary>
    /// 测试相关入口 Commands_WhenDisconnected_ReturnNotConnected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateCard、Equal、ServoOnResult、StopAllResult；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Commands_WhenDisconnected_ReturnNotConnected()
    {
        var card = CreateCard();

        Assert.Equal(EMotionResult.NotConnected, card.ServoOnResult(0).Code);
        Assert.Equal(EMotionResult.NotConnected, card.StopAllResult().Code);
    }

    /// <summary>
    /// 测试相关入口 MotionCommand_InvalidNumber_ReturnsInvalidParam；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、Equal、SetSpeedResult、MoveAbsResult、MoveRelResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 DriverResultCode_IsPreservedAndLegacyBoolUsesSamePath；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、MoveAbsResult、Equal、False、MoveAbs；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 InvalidEnumsAndDisabledHoming_ReturnInvalidParam；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、True、ServoOn、Equal、JogStartResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 OutputAndDisconnect_ReturnDiagnosticResults；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、Equal、SetOutputResult、True、DisconnectResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Simulation_SubPulsePerTickSpeed_StillAccumulatesAndArrives；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、True、ServoOn、SetSpeed、MoveAbs；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Simulation_EmergencyStopBlocksMotionUntilStatusIsCleared；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、True、ServoOn、Stop、ReadAxisStatus；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 CreateCard；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static SimulationMotionCard CreateCard(EHomeMode homeMode = EHomeMode.OriginSwitch)
        => new()
        {
            Param = new MotionCardParam
            {
                Axises = [new AxisParam { HomeMode = homeMode }],
            },
        };

    /// <summary>
    /// 测试相关入口 CreateConnectedCard；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect；具体条件和先后次序见方法体。
    /// </summary>
    private static SimulationMotionCard CreateConnectedCard(EHomeMode homeMode = EHomeMode.OriginSwitch)
    {
        var card = CreateCard(homeMode);
        Assert.True(card.Connect());
        return card;
    }
}

