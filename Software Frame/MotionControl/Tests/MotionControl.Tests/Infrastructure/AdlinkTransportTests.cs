// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AdlinkTransportTests、FakeAdlinkTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.ADLINK、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.ADLINK;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// AdlinkTransportTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class AdlinkTransportTests
{
    /// <summary>
    /// 验证 ConnectMoveAndHome_UseInjectedTransportWithoutNativeSdk 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、ConnectResult、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_TransportFailure_PreservesSpecificResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Connect_TransportFailure_PreservesSpecificResult()
    {
        var transport = new FakeAdlinkTransport { OpenResult = EMotionResult.InvalidParam };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.InvalidParam, result.Code);
        Assert.False(card.IsConnected);
    }

    /// <summary>
    /// 验证 Move_WhenServoIsOff_ReturnsFailRatherThanNotConnected 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、MoveAbsResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Move_TransportFailure_IsReported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、ServoOn、MoveAbsResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 PositionRead_TransportFailure_IsNotReportedAsZero 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、ReadCommandPosition、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_BoardNumberOutsideMaskRange_IsRejectedBeforeOpen 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 CreateCard 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
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

    /// <summary>
    /// FakeAdlinkTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IAdlinkTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 Open 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
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
        public bool SetServo(int axis, bool enabled) => true;
        /// <summary>
        /// 验证 ResetServo 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool ResetServo(int axis, bool reenable) => true;

        /// <summary>
        /// 验证 MoveAbsolute 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool MoveAbsolute(int axis, int pulse, int maximumSpeed)
        {
            LastTargetPulse = pulse;
            LastMaximumSpeed = maximumSpeed;
            return MoveSucceeds;
        }

        /// <summary>
        /// 验证 MoveRelative 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool MoveRelative(int axis, int pulse, int maximumSpeed) => MoveSucceeds;
        /// <summary>
        /// 验证 MoveVelocity 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool MoveVelocity(int axis, int speed) => MoveSucceeds;
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
        /// 验证 StartHome 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool StartHome(int axis)
        {
            StartHomeCount++;
            return true;
        }

        /// <summary>
        /// 验证 SetCurrentPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetCurrentPosition(int axis, int pulse) => true;

        /// <summary>
        /// 验证 TryGetCommandPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryGetCommandPosition(int axis, out int pulse)
        {
            pulse = 0;
            return PositionReadSucceeds;
        }

        /// <summary>
        /// 验证 TryGetEncoderPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryGetEncoderPosition(int axis, out int pulse)
        {
            pulse = 0;
            return true;
        }

        /// <summary>
        /// 验证 GetMotionStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public int GetMotionStatus(int axis) => 0;
        /// <summary>
        /// 验证 GetMotionIoStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public int GetMotionIoStatus(int axis) => 0;

        /// <summary>
        /// 验证 TryReadInput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryReadInput(int index, out bool state)
        {
            state = false;
            return true;
        }

        /// <summary>
        /// 验证 TryReadOutput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryReadOutput(int index, out bool state)
        {
            state = false;
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

