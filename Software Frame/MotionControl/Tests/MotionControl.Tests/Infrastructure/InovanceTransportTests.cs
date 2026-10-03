// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InovanceTransportTests、FakeInovanceTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.Inovance、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// InovanceTransportTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class InovanceTransportTests
{
    /// <summary>
    /// 验证 Connect_TransportFailure_IsReported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Connect_TransportFailure_IsReported()
    {
        var transport = new FakeInovanceTransport { ConnectSucceeds = false };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.Fail, result.Code);
        Assert.False(card.IsConnected);
    }

    /// <summary>
    /// 验证 ConnectAndMove_UseDomainTransportWithoutRegisterKnowledge 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、ConnectResult、Equal、ServoOnResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Move_TargetWriteFailure_DoesNotSendStartCommand 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、ServoOn、MoveAbsResult；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 StopAll_ControlWriteFailure_IsReported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、StopAllResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 SetCurrentAsOrigin_WriteFailure_IsReported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、StartHomeResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 IoAddressOverflow_IsRejectedBeforeTransportCall 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、SetOutputResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 CreateCard 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
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

    /// <summary>
    /// FakeInovanceTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IInovanceTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 Connect 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
        {
            ConnectedIpAddress = ipAddress;
            ConnectedPort = port;
            Parameter = parameter;
            RequiredAxes = requiredAxes;
            IsConnected = ConnectSucceeds;
            return ConnectSucceeds;
        }

        /// <summary>
        /// 验证 Disconnect 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void Disconnect() => IsConnected = false;

        /// <summary>
        /// 验证 TryReadStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryReadStatus(int axis, out ushort status)
        {
            status = 1;
            return true;
        }

        /// <summary>
        /// 验证 UpdateControlWord 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment；具体条件和先后次序见方法体。
        /// </summary>
        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask)
        {
            Interlocked.Increment(ref _controlWordUpdateCount);
            return ControlWriteSucceeds;
        }

        /// <summary>
        /// 验证 WriteTarget 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool WriteTarget(int axis, int position, float velocity)
        {
            LastTargetPosition = position;
            LastTargetVelocity = velocity;
            return TargetWriteSucceeds;
        }

        /// <summary>
        /// 验证 TryReadCurrentPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool TryReadCurrentPosition(int axis, out int position)
        {
            position = 0;
            return true;
        }

        /// <summary>
        /// 验证 SetCurrentPosition 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool SetCurrentPosition(int axis, int position) => CurrentPositionWriteSucceeds;

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
        public bool WriteOutput(int index, bool state)
        {
            OutputWriteCount++;
            return true;
        }

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Disconnect();
    }
}

