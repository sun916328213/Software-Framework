// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DataPointScanPlannerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Communication.Core.Services、Communication.Runtime.Core.Services、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Core.Services;
using Communication.Runtime.Core.Services;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// DataPointScanPlannerTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DataPointScanPlannerTests
{
    /// <summary>
    /// 验证 Only_points_whose_scan_interval_has_elapsed_are_due 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ApplyRead、Plan、AddMilliseconds、FromSeconds、Collection；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Only_points_whose_scan_interval_has_elapsed_are_due()
    {
        var sampledAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        IDataPoint fast = new DataPoint("Fast", DataPointType.Int16, "HR1")
        {
            ScanIntervalMs = 100,
        };
        IDataPoint slow = new DataPoint("Slow", DataPointType.Int16, "HR2")
        {
            ScanIntervalMs = 1000,
        };
        fast.ApplyRead(new byte[] { 0, 1 }, sampledAt);
        slow.ApplyRead(new byte[] { 0, 2 }, sampledAt);

        var plan = DataPointScanPlanner.Plan(
            new[] { slow, fast },
            sampledAt.AddMilliseconds(100),
            TimeSpan.FromSeconds(5));

        Assert.Collection(plan.DuePoints, point => Assert.Same(fast, point));
        Assert.Equal(TimeSpan.Zero, plan.DelayUntilNextScan);
    }

    /// <summary>
    /// 验证 Plan_waits_until_the_nearest_point_deadline 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ApplyRead、Plan、AddMilliseconds、FromSeconds、Empty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Plan_waits_until_the_nearest_point_deadline()
    {
        var sampledAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        IDataPoint fast = new DataPoint("Fast", DataPointType.Int16, "HR1")
        {
            ScanIntervalMs = 100,
        };
        IDataPoint slow = new DataPoint("Slow", DataPointType.Int16, "HR2")
        {
            ScanIntervalMs = 1000,
        };
        fast.ApplyRead(new byte[] { 0, 1 }, sampledAt);
        slow.ApplyRead(new byte[] { 0, 2 }, sampledAt);

        var plan = DataPointScanPlanner.Plan(
            new[] { fast, slow },
            sampledAt.AddMilliseconds(40),
            TimeSpan.FromSeconds(5));

        Assert.Empty(plan.DuePoints);
        Assert.Equal(TimeSpan.FromMilliseconds(60), plan.DelayUntilNextScan);
    }

    /// <summary>
    /// 验证 New_readable_points_are_due_and_zero_interval_inherits_the_default 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToDataPoint、Plan、FromMilliseconds、Collection、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void New_readable_points_are_due_and_zero_interval_inherits_the_default()
    {
        var now = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        IDataPoint inherited = new DataPointDefinition
        {
            Name = "Inherited",
            Address = "HR1",
            ScanIntervalMs = 0,
        }.ToDataPoint();
        IDataPoint disabled = new DataPoint("Disabled", DataPointType.Int16, "HR2")
        {
            Readable = false,
        };

        var firstPlan = DataPointScanPlanner.Plan(
            new[] { inherited, disabled },
            now,
            TimeSpan.FromMilliseconds(250));
        Assert.Collection(firstPlan.DuePoints, point => Assert.Same(inherited, point));

        inherited.ApplyRead(new byte[] { 0, 1 }, now);
        var nextPlan = DataPointScanPlanner.Plan(
            new[] { inherited, disabled },
            now.AddMilliseconds(100),
            TimeSpan.FromMilliseconds(250));
        Assert.Empty(nextPlan.DuePoints);
        Assert.Equal(TimeSpan.FromMilliseconds(150), nextPlan.DelayUntilNextScan);
    }
}
