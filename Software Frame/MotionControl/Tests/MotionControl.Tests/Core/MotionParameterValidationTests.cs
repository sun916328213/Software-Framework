// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MotionParameterValidationTests、TrackingTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.Inovance、MotionControl.Runtime.Vendors.Simulation、MotionControl.Runtime.Vendors.ZMotion、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Inovance;
using MotionControl.Runtime.Vendors.Simulation;
using MotionControl.Runtime.Vendors.ZMotion;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// MotionParameterValidationTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MotionParameterValidationTests
{
    /// <summary>
    /// 测试相关入口 ConnectResult_InvalidCommonAxisParameters_DoNotEnterDriver；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectResult、False、Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ConnectResult_InvalidSoftLimits_ReturnsDiagnosticMessage；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectResult、Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ConnectResult_ValidSimulationConfiguration_ConnectsSuccessfully；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectResult、True、DisConnect；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 MotionCommands_NonFiniteOrOverflowingValues_ReturnFalse；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、Connect、False、SetSpeed、MoveAbs；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ConnectResult_PulseConversionOverflow_IsRejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectResult、Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ConnectedCard_UsesStableSnapshotUntilNextConnection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、Connect、Add、Same、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ParameterSnapshot_PreservesDerivedTypeAndClonesAxes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsType、Snapshot、Add、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ZMotion_MissingIpAddress_IsRejectedBeforeNativeSdkCall；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectResult、Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Inovance_RegisterMapOverflow_IsRejectedBeforeTransportCall；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectResult、Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// TrackingTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IInovanceTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TrackingTransport : IInovanceTransport
    {
        public bool IsConnected => false;
        /// <summary>
        /// 测试相关入口 Connect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
            => throw new InvalidOperationException("参数无效时不应访问传输层。");
        /// <summary>
        /// 测试相关入口 Disconnect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Disconnect() { }
        /// <summary>
        /// 测试相关入口 TryReadStatus；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadStatus(int axis, out ushort status) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 UpdateControlWord；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 WriteTarget；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool WriteTarget(int axis, int position, float velocity) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 TryReadCurrentPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadCurrentPosition(int axis, out int position) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 SetCurrentPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool SetCurrentPosition(int axis, int position) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 TryReadInput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadInput(int index, out bool state) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 TryReadOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadOutput(int index, out bool state) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 WriteOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool WriteOutput(int index, bool state) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Dispose() { }
    }
}

