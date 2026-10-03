// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AlarmHistoryHostTests、DelayedService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Integration.Alarms、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.ViewModels.Alarm、System.IO、System.Windows.Threading、DataManager.Core.Excel.Definition。导入不代表所有类型都会在运行时加载。
using WPFControls.Integration.Alarms;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.ViewModels.Alarm;
using System.IO;
using System.Windows.Threading;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.EPPlus;
using WPFControls.Queries.Alarms;



using Xunit;
using Prism.DryIoc;
using Prism.Ioc;

namespace WPFControls.Tests.Alarms;

/// <summary>
/// AlarmHistoryHostTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class AlarmHistoryHostTests
{
    /// <summary>
    /// 验证 PrismResolvesSharedQueryWithoutEagerlyLoadingAlarmConfiguration 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OnDispatcher、Register、Combine、GetTempPath、ToString；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public Task PrismResolvesSharedQueryWithoutEagerlyLoadingAlarmConfiguration() => OnDispatcher(async () =>
    {
        var directoryReads = 0;
        var container = new DryIocContainerExtension();
        AlarmHistoryComposition.Register(container, _ => { directoryReads++; return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); });
        var service = container.Resolve<IAlarmHistoryQueryService>();
        Assert.Same(service, container.Resolve<IAlarmHistoryQueryService>());
        Assert.Same(container.Resolve<IAlarmHistoryReader>(), container.Resolve<IAlarmHistoryReader>());
        Assert.Equal(0, directoryReads);
        await service.QueryAsync(new(2026, 9, 1), new(2026, 9, 1));
        Assert.Equal(1, directoryReads);
    });

    /// <summary>
    /// 验证 RealExcelHistoryReadsBothFilesWithoutRetargetingLiveWriter 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、CreateDirectory；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RealExcelHistoryReadsBothFilesWithoutRetargetingLiveWriter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlarmHistoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var writer = new EPPlusHelper { ExcelParam = new ExcelParam { FilePath = directory, FileName = "20260901.xlsx" } };
            writer.Write(new[] { new AlarmInfoDTO { ID = 1, Info = "A", StartTime = "s", EndTime = "e", Duration = "1.25 s" } });
            writer.ExcelParam.FileName = "20260902.xlsx";
            writer.Write(new[] { new AlarmInfoDTO { ID = 2, Info = "B", StartTime = "s", EndTime = "e", Duration = "2.50 s" } });
            writer.ExcelParam.FileName = "live.xlsx";
            var service = new AlarmHistoryQueryService(new ExcelAlarmHistoryReader(() => directory));
            var results = await Task.WhenAll(service.QueryAsync(new(2026, 9, 1), new(2026, 9, 2)),
                service.QueryAsync(new(2026, 9, 2), new(2026, 9, 3)));
            Assert.Equal(new[] { "A", "B" }, results[0].Rows.Select(x => x.Info));
            Assert.Equal("B", Assert.Single(results[1].Rows).Info);
            Assert.Equal("live.xlsx", writer.ExcelParam.FileName);
            writer.Add(new AlarmInfoDTO { Info = "live", Duration = "0.00 s" });
            Assert.Equal("live", Assert.Single(writer.Query<AlarmInfoDTO>()).Info);
        }
        finally
        {
            // The exact GUID-owned directory created above is the only cleanup target.
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// 验证 PageStateIsIndependentAndResultNotificationsResumeOnDispatcher 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OnDispatcher、NotSame、Add、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public Task PageStateIsIndependentAndResultNotificationsResumeOnDispatcher() => OnDispatcher(async () =>
    {
        var service = new DelayedService();
        var first = new AlarmInfoQueryViewModel(service);
        var second = new AlarmInfoQueryViewModel(service);
        first.Datas.DateTime_Start = new(2026, 9, 1);
        Assert.NotSame(first.Datas, second.Datas);
        var uiThread = Environment.CurrentManagedThreadId;
        var notifications = new List<int>();
        first.Datas.PropertyChanged += (_, args) => { if (args.PropertyName == "Datas") notifications.Add(Environment.CurrentManagedThreadId); };
        first.PropertyChanged += (_, args) => { if (args.PropertyName == "PieData") notifications.Add(Environment.CurrentManagedThreadId); };
        await first.QueryCommand.ExecuteAsync(null);
        Assert.Equal("A", Assert.Single(first.Datas.Datas).Info);
        Assert.Equal("A", Assert.Single(first.PieData).SeriesName);
        Assert.Empty(second.Datas.Datas);
        Assert.Equal(new[] { uiThread, uiThread }, notifications);
    });

    /// <summary>
    /// 验证 FailedQueryPreservesPreviouslyDisplayedRowsAndChart 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OnDispatcher、ExecuteAsync、ThrowsAsync、Same；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public Task FailedQueryPreservesPreviouslyDisplayedRowsAndChart() => OnDispatcher(async () =>
    {
        var service = new DelayedService();
        var viewModel = new AlarmInfoQueryViewModel(service);
        await viewModel.QueryCommand.ExecuteAsync(null);
        var rows = viewModel.Datas.Datas;
        var chart = viewModel.PieData;
        service.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => viewModel.QueryCommand.ExecuteAsync(null));
        Assert.Same(rows, viewModel.Datas.Datas);
        Assert.Same(chart, viewModel.PieData);
    });

    /// <summary>
    /// DelayedService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IAlarmHistoryQueryService。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class DelayedService : IAlarmHistoryQueryService
    {
        public bool Fail { get; set; }
        /// <summary>
        /// 验证 QueryAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ConfigureAwait、Delay；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task<AlarmHistoryResult> QueryAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            if (Fail) throw new IOException("history read failed");
            return new([new AlarmHistoryRow(1, "A", "start", "end", "1.50 s")], [new("A", 1, 1.5, 1)]);
        }
    }

    /// <summary>
    /// 验证 OnDispatcher 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 SetSynchronizationContext、BeginInvoke、action、TrySetResult、TrySetException；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static async Task OnDispatcher(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception error) { completion.TrySetException(error); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
