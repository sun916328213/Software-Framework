// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LeadshineTransportTests、FakeLeadshineTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.Leadshine、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Leadshine;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// LeadshineTransportTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class LeadshineTransportTests
{
    /// <summary>
    /// 验证 ConnectMoveAndHome_UseInjectedTransportWithoutNativeSdk 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、ConnectResult、Equal、ServoOnResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_TransportFailure_PreservesSpecificResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_ProfileFailure_ClosesTransportAndRollsBack 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Connect_CardNumberOutsideSdkRange_IsRejectedBeforeOpen 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 CreateCard 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
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

    /// <summary>
    /// FakeLeadshineTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILeadshineTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 Open 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public EMotionResult Open(ushort cardNumber, int requiredAxes)
        {
            OpenCount++;
            OpenedCardNumber = cardNumber;
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
        /// 验证 SetProfile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetProfile(int axis, double minimumVelocity, double maximumVelocity,
            double accelerationTime, double decelerationTime, double stopVelocity)
        {
            LastMaximumVelocity = maximumVelocity;
            LastAccelerationTime = accelerationTime;
            return ProfileSucceeds;
        }

        /// <summary>
        /// 验证 SetServo 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetServo(int axis, bool enabled) => true;

        /// <summary>
        /// 验证 MovePosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool MovePosition(int axis, int pulse, bool absolute)
        {
            LastPositionPulse = pulse;
            LastMoveWasAbsolute = absolute;
            return true;
        }

        /// <summary>
        /// 验证 MoveVelocity 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool MoveVelocity(int axis, bool positive) => true;
        /// <summary>
        /// 验证 Stop 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool Stop(int axis, bool emergency) => true;
        /// <summary>
        /// 验证 EmergencyStopAll 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment；具体条件和先后次序见方法体。
        /// </summary>
        public bool EmergencyStopAll()
        {
            Interlocked.Increment(ref _emergencyStopCount);
            return true;
        }

        /// <summary>
        /// 验证 ConfigureHome 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool ConfigureHome(int axis, ushort direction, double velocity, ushort mode,
            bool enableLimitReturn, int? offsetPulse)
        {
            LastHomeDirection = direction;
            LastHomeLimitReturnEnabled = enableLimitReturn;
            LastHomeOffsetPulse = offsetPulse;
            return true;
        }

        /// <summary>
        /// 验证 StartHome 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool StartHome(int axis) => true;

        /// <summary>
        /// 验证 TryGetHomeResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryGetHomeResult(int axis, out ushort state)
        {
            state = 0;
            return true;
        }

        /// <summary>
        /// 验证 CheckDone 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public short CheckDone(int axis) => 1;
        /// <summary>
        /// 验证 GetAxisIoStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public uint GetAxisIoStatus(int axis) => 0;
        /// <summary>
        /// 验证 GetCommandPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public int GetCommandPosition(int axis) => 0;
        /// <summary>
        /// 验证 GetEncoderPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public int GetEncoderPosition(int axis) => 0;
        /// <summary>
        /// 验证 SetCurrentPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetCurrentPosition(int axis, int pulse) => true;
        /// <summary>
        /// 验证 ReadInput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public short ReadInput(int index) => 0;
        /// <summary>
        /// 验证 ReadOutput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public short ReadOutput(int index) => 0;
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

