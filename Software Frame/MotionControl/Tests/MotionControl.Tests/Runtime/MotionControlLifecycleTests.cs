// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MotionControlLifecycleTests、BlockingMotionCard。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions、MotionControl.Runtime.Services、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// MotionControlLifecycleTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MotionControlLifecycleTests
{
    /// <summary>
    /// 测试相关入口 Disconnect_WaitsForInFlightCommandBeforeReleasingConnection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、Connect、Run、MoveAbsResult、Wait；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Disconnect_WaitsForInFlightCommandBeforeReleasingConnection()
    {
        var card = new BlockingMotionCard
        {
            Param = new MotionCardParam { Axises = [new AxisParam()] },
        };
        Assert.True(card.Connect());

        var move = Task.Run(() => card.MoveAbsResult(0, 1));
        Assert.True(card.MoveEntered.Wait(TimeSpan.FromSeconds(2)));

        var disconnect = Task.Run(card.DisconnectResult);
        Assert.False(card.DisconnectEntered.Wait(TimeSpan.FromMilliseconds(150)));

        card.ReleaseMove.Set();
        var moveResult = await move;
        var disconnectResult = await disconnect;

        Assert.True(moveResult.IsSuccess);
        Assert.True(disconnectResult.IsSuccess);
        Assert.Equal(["move-start", "move-end", "disconnect"], card.Events);
        Assert.False(card.IsConnected);
    }

    /// <summary>
    /// BlockingMotionCard 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：MotionControlBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class BlockingMotionCard : MotionControlBase
    {
        public ManualResetEventSlim MoveEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseMove { get; } = new(false);
        public ManualResetEventSlim DisconnectEntered { get; } = new(false);
        public List<string> Events { get; } = new();

        /// <summary>
        /// 测试相关入口 ConnectRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult ConnectRefactor() => EMotionResult.Success;

        /// <summary>
        /// 测试相关入口 DisConnectRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Set、Add；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult DisConnectRefactor()
        {
            DisconnectEntered.Set();
            lock (Events)
                Events.Add("disconnect");
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 ServoOnRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult ServoOnRefactor(int axis) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 ServoOffRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult ServoOffRefactor(int axis) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 ClearAxisStatusRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult ClearAxisStatusRefactor(int axis) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 SetSpeedRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult SetSpeedRefactor(int axis, long velPulse) => EMotionResult.Success;

        /// <summary>
        /// 测试相关入口 MoveAbsRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add、Set、Wait、FromSeconds；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        protected override EMotionResult MoveAbsRefactor(int axis, long pulse)
        {
            lock (Events)
                Events.Add("move-start");
            MoveEntered.Set();
            if (!ReleaseMove.Wait(TimeSpan.FromSeconds(3)))
                return EMotionResult.Timeout;
            lock (Events)
                Events.Add("move-end");
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 MoveRelRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult MoveRelRefactor(int axis, long pulse) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 JogStartRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult JogStartRefactor(int axis, short direction) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 StopAxisRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 StopAllRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult StopAllRefactor(EStopMode stopMode) => EMotionResult.Success;
        /// <summary>
        /// 测试相关入口 HomeStartRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult HomeStartRefactor(int axis) => EMotionResult.Success;

        /// <summary>
        /// 测试相关入口 GetCommandPositionRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult GetCommandPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 GetEncoderPositionRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 GetAxisStatusRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = (long)EAxisStatusFlag.InPosition;
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 GetInputRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult GetInputRefactor(int index, out bool state)
        {
            state = false;
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 GetOutputRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            state = false;
            return EMotionResult.Success;
        }

        /// <summary>
        /// 测试相关入口 SetOutputRefactor；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override EMotionResult SetOutputRefactor(int index, bool state) => EMotionResult.Success;
    }
}


