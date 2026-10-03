// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：GoogolTransportTests、FakeGoogolTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.Googol、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Googol;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// GoogolTransportTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class GoogolTransportTests
{
    /// <summary>
    /// 验证 ConnectMoveAndHome_UseInjectedTransportWithoutNativeSdk 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、ConnectResult、Equal、ServoOnResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_TransportFailure_PreservesSpecificResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Connect_TransportFailure_PreservesSpecificResult()
    {
        var transport = new FakeGoogolTransport { OpenResult = EMotionResult.InvalidParam };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.False(card.IsConnected);
    }

    /// <summary>
    /// 验证 Move_TransportFailure_IsReported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、ServoOn、MoveAbsResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 PositionRead_TransportFailure_IsNotReportedAsZero 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、ReadCommandPosition、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_ChannelOutsideSdkRange_IsRejectedBeforeOpen 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 CreateCard 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
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

    /// <summary>
    /// FakeGoogolTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IGoogolTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 Open 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public EMotionResult Open(short channel, string? configFile, int requiredAxes)
        {
            OpenCount++;
            OpenedChannel = channel;
            ConfigFile = configFile;
            RequiredAxes = requiredAxes;
            IsOpen = OpenResult == EMotionResult.Success;
            return OpenResult;
        }

        /// <summary>
        /// 验证 Close 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            CloseCount++;
        }

        /// <summary>
        /// 验证 SetServo 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetServo(int axis, bool enabled)
        {
            if (enabled)
                _status |= GTS.GTS_STS_SERVO_ON;
            else
                _status &= ~GTS.GTS_STS_SERVO_ON;
            return true;
        }

        /// <summary>
        /// 验证 ClearStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool ClearStatus(int axis) => true;

        /// <summary>
        /// 验证 StartTrapMove 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool StartTrapMove(int axis, int targetPulse, double acceleration, double deceleration,
            double startVelocity, double maximumVelocity)
        {
            LastTargetPulse = targetPulse;
            LastMaximumVelocity = maximumVelocity;
            LastAcceleration = acceleration;
            LastStartVelocity = startVelocity;
            return TrapMoveSucceeds;
        }

        /// <summary>
        /// 验证 StartJog 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool StartJog(int axis, double acceleration, double deceleration, double velocity) => true;
        /// <summary>
        /// 验证 StopAxis 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool StopAxis(int axis, bool emergency) => true;

        /// <summary>
        /// 验证 StopAll 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment；具体条件和先后次序见方法体。
        /// </summary>
        public bool StopAll(int axisCount, bool emergency)
        {
            Interlocked.Increment(ref _stopAllCount);
            return true;
        }

        /// <summary>
        /// 验证 TryGetStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Read；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetStatus(int axis, out int status)
        {
            status = Volatile.Read(ref _status);
            return true;
        }

        /// <summary>
        /// 验证 TryGetCommandPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryGetCommandPosition(int axis, out double pulse)
        {
            pulse = 0;
            return PositionReadSucceeds;
        }

        /// <summary>
        /// 验证 TryGetEncoderPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryGetEncoderPosition(int axis, out double pulse)
        {
            pulse = 0;
            return true;
        }

        /// <summary>
        /// 验证 SetCurrentAsOrigin 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetCurrentAsOrigin(int axis) => true;
        /// <summary>
        /// 验证 SetCommandPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetCommandPosition(int axis, int pulse) => true;

        /// <summary>
        /// 验证 StartHome 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool StartHome(int axis, GoogolHomeProfile profile)
        {
            LastHomeProfile = profile;
            return true;
        }

        /// <summary>
        /// 验证 TryGetHomeState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryGetHomeState(int axis, out GoogolHomeState state)
        {
            state = new GoogolHomeState(0, 0);
            return true;
        }

        /// <summary>
        /// 验证 TryReadInputs 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryReadInputs(out long value)
        {
            value = 0;
            return true;
        }

        /// <summary>
        /// 验证 TryReadOutputs 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryReadOutputs(out long value)
        {
            value = 0;
            return true;
        }

        /// <summary>
        /// 验证 WriteOutput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool WriteOutput(int index, bool state) => true;
        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Close();
    }
}

