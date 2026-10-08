// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MotionResultTests、FakeInovanceTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.Inovance、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// MotionResultTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MotionResultTests
{
    /// <summary>
    /// 测试相关入口 DefaultResult_IsNeverSuccessful；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ReadInput_LowLevel_ReturnsSuccessfulFalseInsteadOfFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、ReadInput、True、False、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ReadInput_TransportFailure_IsNotReportedAsLowLevel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、ReadInput、False、Equal、DisConnect；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ReadCommandPosition_InvalidAxis_ReturnsExplicitError；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、ReadCommandPosition、False、Equal、DisConnect；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ReadCommandPosition_TransportFailure_IsNotReportedAsZeroPosition；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、ReadCommandPosition、False、Equal、DisConnect；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 WaitInPosition_CancelledToken_ReturnsCancelledResult；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateConnectedCard、Cancel、WaitInPositionResultAsync、False、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 CreateConnectedCard；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、Connect；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// FakeInovanceTransport 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IInovanceTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FakeInovanceTransport : IInovanceTransport
    {
        public bool FailReads { get; set; }
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 测试相关入口 Connect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool Connect(string ipAddress, int port, InovanceParam parameter, int requiredAxes)
        {
            IsConnected = true;
            return true;
        }

        /// <summary>
        /// 测试相关入口 Disconnect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Disconnect() => IsConnected = false;

        /// <summary>
        /// 测试相关入口 TryReadStatus；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadStatus(int axis, out ushort status)
        {
            status = 0;
            return !FailReads;
        }

        /// <summary>
        /// 测试相关入口 UpdateControlWord；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool UpdateControlWord(int axis, ushort setBits, ushort clearMask) => true;
        /// <summary>
        /// 测试相关入口 WriteTarget；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool WriteTarget(int axis, int position, float velocity) => true;

        /// <summary>
        /// 测试相关入口 TryReadCurrentPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadCurrentPosition(int axis, out int position)
        {
            position = 0;
            return !FailReads;
        }

        /// <summary>
        /// 测试相关入口 SetCurrentPosition；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool SetCurrentPosition(int axis, int position) => true;

        /// <summary>
        /// 测试相关入口 TryReadInput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadInput(int index, out bool state)
        {
            state = false;
            return !FailReads;
        }

        /// <summary>
        /// 测试相关入口 TryReadOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool TryReadOutput(int index, out bool state)
        {
            state = false;
            return !FailReads;
        }

        /// <summary>
        /// 测试相关入口 WriteOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public bool WriteOutput(int index, bool state) => true;
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Disconnect();
    }
}

