// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AxisPollingMonitorTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics、MotionControl.Runtime.Internal、Xunit。导入不代表所有类型都会在运行时加载。
using System.Diagnostics;
using MotionControl.Runtime.Internal;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// AxisPollingMonitorTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class AxisPollingMonitorTests
{
    /// <summary>
    /// 测试相关入口 PollingException_WithoutObserver_IsReportedToTrace；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、Increment、TrySetResult、Start、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task PollingException_WithoutObserver_IsReportedToTrace()
    {
        using var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var reads = 0;
            var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var monitor = new AxisPollingMonitor(
                1,
                _ =>
                {
                    if (Interlocked.Increment(ref reads) == 1)
                        throw new InvalidOperationException("poll fault");
                    return new AxisMonitorSample(0, false);
                },
                (_, _) => { },
                (_, _) => recovered.TrySetResult());

            monitor.Start(1);
            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            monitor.Stop();
            listener.Flush();

            Assert.Contains("poll fault", output.ToString());
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    /// <summary>
    /// 测试相关入口 AlarmRaised_OnlyOnRisingEdge；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Increment、Read、TrySetResult、Start、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task AlarmRaised_OnlyOnRisingEdge()
    {
        var reads = 0;
        var alarms = 0;
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = new AxisPollingMonitor(
            1,
            _ => new AxisMonitorSample(123, Interlocked.Increment(ref reads) >= 2),
            (_, _) => Interlocked.Increment(ref alarms),
            (_, _) =>
            {
                if (Volatile.Read(ref reads) >= 5)
                    observed.TrySetResult();
            });

        monitor.Start(1);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        monitor.Stop();

        Assert.Equal(1, Volatile.Read(ref alarms));
    }

    /// <summary>
    /// 测试相关入口 PollingException_IsolatedAndNextCycleContinues；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Increment、TrySetResult、Start、WaitAsync、FromSeconds；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task PollingException_IsolatedAndNextCycleContinues()
    {
        var reads = 0;
        var errors = 0;
        var alarm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = new AxisPollingMonitor(
            1,
            _ =>
            {
                if (Interlocked.Increment(ref reads) == 1)
                    throw new InvalidOperationException("transient");
                return new AxisMonitorSample(456, true);
            },
            (_, _) => alarm.TrySetResult(),
            errorObserved: _ => Interlocked.Increment(ref errors));

        monitor.Start(1);
        await alarm.Task.WaitAsync(TimeSpan.FromSeconds(2));
        monitor.Stop();

        Assert.True(Volatile.Read(ref reads) >= 2);
        Assert.Equal(1, Volatile.Read(ref errors));
    }

    /// <summary>
    /// 测试相关入口 Stop_SuppressesCallbacksFromInFlightRead；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Set、Wait、FromSeconds、TrySetResult、Increment；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Stop_SuppressesCallbacksFromInFlightRead()
    {
        using var readStarted = new ManualResetEventSlim();
        using var releaseRead = new ManualResetEventSlim();
        var readReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alarms = 0;
        var observations = 0;
        using var monitor = new AxisPollingMonitor(
            1,
            _ =>
            {
                readStarted.Set();
                releaseRead.Wait(TimeSpan.FromSeconds(2));
                readReturned.TrySetResult();
                return new AxisMonitorSample(789, true);
            },
            (_, _) => Interlocked.Increment(ref alarms),
            (_, _) => Interlocked.Increment(ref observations));

        monitor.Start(1);
        Assert.True(readStarted.Wait(TimeSpan.FromSeconds(2)));

        monitor.Stop();
        releaseRead.Set();
        await readReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(25);

        Assert.Equal(0, Volatile.Read(ref alarms));
        Assert.Equal(0, Volatile.Read(ref observations));
    }
}

