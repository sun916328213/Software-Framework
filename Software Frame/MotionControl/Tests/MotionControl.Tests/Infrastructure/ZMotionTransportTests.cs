// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ZMotionTransportTests、FakeZMotionTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.ZMotion、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.ZMotion;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// ZMotionTransportTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ZMotionTransportTests
{
    /// <summary>
    /// 测试相关入口 ConnectAndMove_UseInjectedTransportWithoutNativeSdk；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、ConnectResult、Equal、ServoOnResult；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ConnectAndMove_UseInjectedTransportWithoutNativeSdk()
    {
        var transport = new FakeZMotionTransport();
        var card = CreateCard(transport);

        try
        {
            Assert.True(card.ConnectResult().IsSuccess);
            Assert.Equal("127.0.0.1", transport.ConnectedIpAddress);
            Assert.Equal((uint)502, transport.ConnectedPort);
            Assert.Equal([0], transport.ConfiguredAxes);

            Assert.True(card.ServoOnResult(0).IsSuccess);
            Assert.True(card.SetSpeedResult(0, 12).IsSuccess);
            Assert.True(card.MoveAbsResult(0, 3).IsSuccess);

            Assert.Equal(3000, transport.LastAbsolutePulse);
            Assert.Equal(12000f, transport.LastSpeed);
            Assert.Equal(1_000_000f, transport.LastAcceleration);
            Assert.Equal(1_000_000f, transport.LastDeceleration);
        }
        finally
        {
            card.DisConnect();
        }

        Assert.False(transport.IsConnected);
        Assert.True(transport.CancelCount > 0);
    }

    /// <summary>
    /// 测试相关入口 Connect_TransportFailure_ReturnsFail；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateCard、ConnectResult、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Connect_TransportFailure_ReturnsFail()
    {
        var transport = new FakeZMotionTransport { ConnectSucceeds = false };
        var card = CreateCard(transport);

        var result = card.ConnectResult();

        Assert.Equal(EMotionResult.Fail, result.Code);
        Assert.False(card.IsConnected);
    }

    /// <summary>
    /// 测试相关入口 PositionRead_TransportFailure_IsNotReportedAsZero；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateCard、True、Connect、ReadCommandPosition、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PositionRead_TransportFailure_IsNotReportedAsZero()
    {
        var transport = new FakeZMotionTransport();
        var card = CreateCard(transport);
        Assert.True(card.Connect());
        transport.CommandPositionReadSucceeds = false;

        try
        {
            var result = card.ReadCommandPosition(0);

            Assert.Equal(EMotionResult.Fail, result.Code);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            transport.CommandPositionReadSucceeds = true;
            card.DisConnect();
        }
    }

    /// <summary>
    /// 测试相关入口 CreateCard；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static ZMotionHelper CreateCard(FakeZMotionTransport transport)
        => new(transport)
        {
            Param = new MotionCardParam
            {
                IpAddress = "127.0.0.1",
                TcpPort = 502,
                Axises = [new AxisParam()],
            },
        };

    /// <summary>
    /// FakeZMotionTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IZMotionTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FakeZMotionTransport : IZMotionTransport
    {
        public bool ConnectSucceeds { get; set; } = true;
        public bool CommandPositionReadSucceeds { get; set; } = true;
        public bool IsConnected { get; private set; }
        public string? ConnectedIpAddress { get; private set; }
        public uint ConnectedPort { get; private set; }
        public List<int> ConfiguredAxes { get; } = new();
        public long LastAbsolutePulse { get; private set; }
        public float LastSpeed { get; private set; }
        public float LastAcceleration { get; private set; }
        public float LastDeceleration { get; private set; }
        public int CancelCount => Volatile.Read(ref _cancelCount);

        /// <summary>
        /// 测试相关入口 Connect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool Connect(string ipAddress, uint port)
        {
            ConnectedIpAddress = ipAddress;
            ConnectedPort = port;
            IsConnected = ConnectSucceeds;
            return ConnectSucceeds;
        }

        /// <summary>
        /// 测试相关入口 Disconnect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Disconnect() => IsConnected = false;

        /// <summary>
        /// 测试相关入口 ConfigureAxis；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public bool ConfigureAxis(int axis)
        {
            ConfiguredAxes.Add(axis);
            return true;
        }

        /// <summary>
        /// 测试相关入口 SetMotionProfile；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool SetMotionProfile(int axis, float speed, float acceleration, float deceleration)
        {
            LastSpeed = speed;
            LastAcceleration = acceleration;
            LastDeceleration = deceleration;
            return true;
        }

        /// <summary>
        /// 测试相关入口 MoveAbsolute；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool MoveAbsolute(int axis, long pulse)
        {
            LastAbsolutePulse = pulse;
            return true;
        }

        /// <summary>
        /// 测试相关入口 MoveRelative；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool MoveRelative(int axis, long pulse) => true;
        /// <summary>
        /// 测试相关入口 Jog；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool Jog(int axis, int direction) => true;

        /// <summary>
        /// 测试相关入口 Cancel；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Increment；具体条件和先后次序见方法体。
        /// </summary>
        public bool Cancel(int axis, int mode)
        {
            Interlocked.Increment(ref _cancelCount);
            return true;
        }

        private int _cancelCount;

        /// <summary>
        /// 测试相关入口 Datum；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool Datum(int axis, int mode) => true;
        /// <summary>
        /// 测试相关入口 SetCommandPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool SetCommandPosition(int axis, float pulse) => true;

        /// <summary>
        /// 测试相关入口 TryGetCommandPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryGetCommandPosition(int axis, out float pulse)
        {
            pulse = 0;
            return CommandPositionReadSucceeds;
        }

        /// <summary>
        /// 测试相关入口 TryGetFeedbackPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryGetFeedbackPosition(int axis, out float pulse)
        {
            pulse = 0;
            return true;
        }

        /// <summary>
        /// 测试相关入口 TryGetAxisStatus；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryGetAxisStatus(int axis, out int status)
        {
            status = 0;
            return true;
        }

        /// <summary>
        /// 测试相关入口 TryGetIdle；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryGetIdle(int axis, out int idle)
        {
            idle = 0;
            return true;
        }

        /// <summary>
        /// 测试相关入口 TryGetInput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryGetInput(int index, out uint value)
        {
            value = 0;
            return true;
        }

        /// <summary>
        /// 测试相关入口 TryGetOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryGetOutput(int index, out uint value)
        {
            value = 0;
            return true;
        }

        /// <summary>
        /// 测试相关入口 SetOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool SetOutput(int index, uint value) => true;
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Disconnect();
    }
}

