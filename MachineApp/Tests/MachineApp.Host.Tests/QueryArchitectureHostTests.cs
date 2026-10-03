// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：QueryArchitectureHostTests、CapacityReader、LogReader、DataRow、TableReader、SemiReader、SqlProbe。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections、System.Reflection、System.Linq.Expressions、System.Windows、System.Windows.Controls、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using System.Collections;
using System.Reflection;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Regions;
using WPFControls.Queries.Data;
using WPFControls.Queries.Logs;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using WPFControls.ProgramControl.ViewModels;
using WPFControls.ProgramControl.Views;
using Xunit;
using MachineApp.Infra.Persistence;
using MachineApp.Infra.Persistence.Entities;

namespace MachineApp.Host.Tests;

/// <summary>
/// QueryArchitectureHostTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class QueryArchitectureHostTests(ProductionFixture fixture)
{
    /// <summary>
    /// 验证 ProductionQueryPagesKeepNavigationBindingsAndRendering 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Add、RequestNavigate、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProductionQueryPagesKeepNavigationBindingsAndRendering()
    {
        fixture.Invoke(container =>
        {
            var manager = container.Resolve<IRegionManager>();
            var region = new Region();
            manager.Regions.Add("S3QueryAcceptance", region);
            foreach (var name in new[] { "CapacityStatisticsView", "LogQueryView", "SEMILogQueryView" })
            {
                NavigationResult? result = null;
                manager.RequestNavigate("S3QueryAcceptance", name, navigation => result = navigation);
                Assert.True(result?.Result, result?.Error?.ToString());
                var view = Assert.IsAssignableFrom<FrameworkElement>(Assert.Single(region.ActiveViews, active => active.GetType().Name == name));
                Assert.NotNull(view.DataContext);
                Assert.NotNull(Get(view.DataContext, "Datas"));
                Assert.NotNull(Get(view.DataContext, "QueryCommand"));
                Assert.NotNull(Get(view.DataContext, "SaveCommand"));
                Render(view);
            }
            Assert.Same(container.Resolve<ICapacityStatisticsQueryService<CapacityStatisticsRow>>(),
                container.Resolve<ICapacityStatisticsQueryService<CapacityStatisticsRow>>());
            Assert.Same(container.Resolve<ILogHistoryQueryService<LogInfo>>(), container.Resolve<ILogHistoryQueryService<LogInfo>>());
            Assert.Same(container.Resolve<ILogHistoryQueryService<string>>(), container.Resolve<ILogHistoryQueryService<string>>());
        });
    }

    /// <summary>
    /// 验证 CapacityReplacementFlowsThroughTheRealCommandAndChartBindingsWithoutSql 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Container、RegisterInstance、RegisterCapacity、RegisterTypes；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CapacityReplacementFlowsThroughTheRealCommandAndChartBindingsWithoutSql()
    {
        fixture.Invoke(production =>
        {
            var day = new DateTime(2026, 10, 1);
            var row = new CapacityStatisticsRow { Time = day, ID = "20261001", OKCOunt1 = 3, NGCount24 = 1 };
            var reader = new CapacityReader(row);
            var container = Container(production);
            container.RegisterInstance<ICapacityStatisticsReader<CapacityStatisticsRow>>(reader);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            Assert.Same(reader, container.Resolve<ICapacityStatisticsReader<CapacityStatisticsRow>>());
            var service = container.Resolve<CapacityStatisticsService>();
            Assert.Same(service, container.Resolve<CapacityStatisticsService>());
            var model = new CapacityStatisticsViewModel(service);
            model.Datas.StartTime = model.Datas.EndTime = day;
            var collection = model.Datas.CapacityStatisticsDatas;
            var view = new CapacityStatisticsView { DataContext = model };
            model.QueryCommand.Execute(null);
            Assert.Equal(1, reader.Calls);
            Assert.Same(collection, model.Datas.CapacityStatisticsDatas);
            Assert.Same(row, Assert.Single(collection));
            Assert.Equal((4, 3, 1, 75.0, 2.0, 4.17), (model.Datas.TotalOutput, model.Datas.GoodCount,
                model.Datas.DefectCount, model.Datas.YieldRate, model.Datas.RunTime, model.Datas.UtilizationRate));
            Assert.Equal(new[] { "OK", "NG", "Total" }, model.Datas.OutputTrend.Select(series => series.SeriesName));
            Assert.Equal(3, model.Datas.OutputTrend[0].Value[0]);
            Assert.Equal(1, model.Datas.OutputTrend[1].Value[23]);
            Render(view);
            // The injected constructor can be used without resolving a concrete SQL instance.
            Assert.NotNull(new CapacityStatisticsService(null,
                container.Resolve<ICapacityStatisticsQueryService<CapacityStatisticsRow>>(),
                container.Resolve<ICapacityStatisticsWriteService<CapacityStatisticsRow>>()));
        });
    }

    /// <summary>
    /// 验证 LogReplacementKeepsCommandsPageResetFilteringAndBoundDataGrid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、ToArray、Select、Range、ToString；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LogReplacementKeepsCommandsPageResetFilteringAndBoundDataGrid()
    {
        fixture.Invoke(production =>
        {
            var reader = new LogReader(Enumerable.Range(0, 25).Select(index =>
                new LogInfo(index.ToString(), index % 2 == 0 ? "Alarm A" : "Alarm B", ELogLevel.Warning)).ToArray());
            var container = Container(production);
            container.RegisterInstance<ILogHistoryReader<LogInfo>>(reader);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            var view = Assert.IsAssignableFrom<FrameworkElement>(container.Resolve<object>("LogQueryView"));
            view.DataContext = container.Resolve(typeof(CapacityStatisticsService).Assembly.GetType("WPFControls.ProgramControl.ViewModels.Log.LogQueryViewModel")!);
            var vm = view.DataContext ?? throw new InvalidOperationException("No log ViewModel");
            ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)Get(vm, "QueryCommand")).ExecuteAsync(null).GetAwaiter().GetResult();
            var model = Get(vm, "Datas");
            Assert.Equal(1, reader.Calls);
            Assert.Equal(25, Get(model, "TotalCount"));
            Assert.Equal(20, ((ICollection)Get(model, "LogDatas")).Count);
            var legacy = vm.GetType().GetField("Service", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
            var pie = (WPFControls.Controls.Services.Charts.ChartsPieService)legacy.GetType()
                .GetProperty("Service_PieCharts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(legacy)!;
            Assert.Equal(new[] { "Alarm A", "Alarm B" }, pie.GetSeries().Select(series => series.Name));
            Assert.Equal(new[] { 10.0, 10.0 }, pie.GetSeries().Select(series =>
                ((IEnumerable)series.GetType().GetProperty("Values")!.GetValue(series)!).Cast<object>().Sum(Convert.ToDouble)));
            ((System.Windows.Input.ICommand)Get(vm, "PageChangedCommand")).Execute(2);
            Assert.Equal(5, ((ICollection)Get(model, "LogDatas")).Count);
            Set(model, "FilterItem", "Info"); Set(model, "FilterText", "alarm a");
            ((System.Windows.Input.ICommand)Get(vm, "FilterCommand")).Execute(null);
            Assert.Equal(13, Get(model, "TotalCount"));
            Assert.Equal(1, Get(model, "CurrentPage"));
            ((System.Windows.Input.ICommand)Get(vm, "FilterResetCommand")).Execute(null);
            Assert.Equal(25, Get(model, "TotalCount"));
            Set(model, "SelectItem", ELogLevel.Information);
            ((System.Windows.Input.ICommand)Get(vm, "SelectFilterCommand")).Execute(null);
            Assert.Equal(0, ((ICollection)Get(model, "LogDatas")).Count);
            ((System.Windows.Input.ICommand)Get(vm, "SelectResetCommand")).Execute(null);
            Assert.Equal(20, ((ICollection)Get(model, "LogDatas")).Count);
            Render(view);
            Assert.Contains(Descendants(view).OfType<DataGrid>(), grid => grid.Items.Count == 20);
            Assert.Same(reader, container.Resolve<ILogHistoryReader<LogInfo>>());
        });
    }

    /// <summary>
    /// 验证 GenericDataPageUsesTheInjectedQueryAndRetainsExistingRowsOnEmptyResults 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Container、RegisterInstance、RegisterCapacity、RegisterTypes；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void GenericDataPageUsesTheInjectedQueryAndRetainsExistingRowsOnEmptyResults()
    {
        fixture.Invoke(production =>
        {
            var reader = new TableReader();
            var container = Container(production);
            container.RegisterInstance<ITableQueryReader<DataRow>>(reader);
            var model = new DataQueryModel<DataRow, DataRow>();
            container.RegisterInstance(model);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            var service = container.Resolve<DataQueryService<DataRow, DataRow>>();
            var vm = new DataQueryViewModel<DataRow, DataRow>(service);
            var view = new DataQueryView { DataContext = vm };
            service.Query_DB();
            PumpUntil(() => model.DatasDTO.Count == 2);
            Assert.Equal(new[] { "one", "two" }, model.DatasDTO.Select(row => row.ID));
            reader.Rows = [];
            service.Query_DB();
            PumpUntil(() => reader.Calls == 2);
            Assert.Equal(2, model.DatasDTO.Count);
            Render(view);
            var grid = Assert.Single(Descendants(view).OfType<DataGrid>());
            Assert.Same(vm, grid.DataContext);
            var binding = BindingOperations.GetBindingExpression(grid, ItemsControl.ItemsSourceProperty);
            Assert.NotNull(binding);
            // Verify the real binding path/value; automatic delivery timing remains an existing business-stage issue.
            binding!.UpdateTarget();
            Assert.Same(model.DatasDTO, grid.ItemsSource);
            Assert.Equal(2, grid.Items.Count);
        });
    }

    /// <summary>
    /// 验证 SemiLogReplacementPreservesRawRowsInTheRealView 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Container、RegisterInstance、RegisterCapacity、RegisterTypes；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SemiLogReplacementPreservesRawRowsInTheRealView()
    {
        fixture.Invoke(production =>
        {
            var container = Container(production);
            var reader = new SemiReader();
            container.RegisterInstance<ILogHistoryReader<string>>(reader);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            var view = Assert.IsAssignableFrom<FrameworkElement>(container.Resolve<object>("SEMILogQueryView"));
            view.DataContext = container.Resolve(typeof(CapacityStatisticsService).Assembly.GetType("WPFControls.ProgramControl.ViewModels.Log.SEMILogQueryViewModel")!);
            var vm = view.DataContext ?? throw new InvalidOperationException("No SEMI ViewModel");
            ((System.Windows.Input.ICommand)Get(vm, "QueryCommand")).Execute(null);
            var rows = (IEnumerable<string>)Get(Get(vm, "Datas"), "SEMILogDatas");
            Assert.Equal(new[] { "SEMI,1", "", "SEMI,1" }, rows);
            Assert.Equal(1, reader.Calls);
            Render(view);
        });
    }

    /// <summary>
    /// 验证 DefaultCapacityReaderResolvesLateSqlAndKeepsAllHourlyFieldsAndWrites 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Container、RegisterCapacity、RegisterTypes、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultCapacityReaderResolvesLateSqlAndKeepsAllHourlyFieldsAndWrites()
    {
        fixture.Invoke(production =>
        {
            var container = Container(production);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            var service = container.Resolve<CapacityStatisticsService>();
            var sql = DispatchProxy.Create<ISQL<CapacityStatisticsInfo>, SqlProbe<CapacityStatisticsInfo>>();
            var probe = (SqlProbe<CapacityStatisticsInfo>)(object)sql;
            container.RegisterInstance(sql);
            var day = new DateTime(2026, 10, 1);
            for (int hour = 0; hour < 24; hour++)
            {
                Assert.True(service.SaveStatistics(day.AddHours(hour), true));
                Assert.True(service.SaveStatistics(day.AddHours(hour), false));
            }
            var saved = Assert.Single(probe.Rows);
            for (int index = 1; index <= 24; index++)
            {
                Assert.Equal(1, typeof(CapacityStatisticsRow).GetProperty($"OKCOunt{index}")!.GetValue(saved));
                Assert.Equal(1, typeof(CapacityStatisticsRow).GetProperty($"NGCount{index}")!.GetValue(saved));
            }
            probe.Rows.Add(new() { ID = "next", Time = day.AddDays(1).AddHours(15), OKCOunt24 = 2 });
            probe.Rows.Add(new() { ID = "outside", Time = day.AddDays(2), OKCOunt1 = 100 });
            service.Model.StartTime = service.Model.EndTime = day;
            service.GetStatistics();
            Assert.Equal((26, 24, 50), (service.Model.GoodCount, service.Model.DefectCount, service.Model.TotalOutput));
            Assert.Equal(2, service.Model.CapacityStatisticsDatas.Count);
            Assert.True(service.SaveStatistics(new CapacityStatisticsRow { Time = day.AddDays(1), OKCOunt24 = 7, NGCount24 = 3 }));
            Assert.Equal(7, saved.OKCOunt24);
            Assert.Equal(3, saved.NGCount24);
            Assert.Equal(1, saved.OKCOunt1);
        });
    }

    /// <summary>
    /// 验证 DefaultTableAdapterKeepsInclusiveDatesExactIdAndManagerIndexZero 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Container、Create、AddRange、AddHours；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultTableAdapterKeepsInclusiveDatesExactIdAndManagerIndexZero()
    {
        fixture.Invoke(production =>
        {
            var container = Container(production);
            var sql = DispatchProxy.Create<ISQL<DataRow>, SqlProbe<DataRow>>();
            var probe = (SqlProbe<DataRow>)(object)sql;
            var day = new DateTime(2026, 10, 1);
            probe.Rows.AddRange([new() { ID = "a", Time = day }, new() { ID = "a", Time = day.AddDays(1).AddHours(16) },
                new() { ID = "b", Time = day }, new() { ID = "a", Time = day.AddDays(2) }]);
            var manager = new SQLManager(); manager.Add(sql);
            container.RegisterInstance(manager);
            PackingPersistenceComposition.RegisterCapacity(container);
            new WPFControls.WPFControlsModule().RegisterTypes(container);
            var query = container.Resolve<ITableQueryService<DataRow>>();
            Assert.Equal(3, query.Query(day.AddHours(3), day.AddHours(4), "").Count);
            Assert.Equal(2, query.Query(day, day, "a").Count);
            Assert.Empty(query.Query(day, day, " a "));
        });
    }

    /// <summary>
    /// 验证 Container 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、Resolve、Fail、ToString；具体条件和先后次序见方法体。
    /// </summary>
    private static DryIocContainerExtension Container(IContainerProvider production)
    {
        var container = new DryIocContainerExtension();
        container.RegisterInstance(production.Resolve<ILogManager>());
        container.RegisterInstance(new TryCatchHelper(error => Assert.Fail(error.ToString())));
        return container;
    }
    /// <summary>
    /// 验证 Get 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetValue、GetProperty、GetType；具体条件和先后次序见方法体。
    /// </summary>
    private static object Get(object target, string property) => target.GetType().GetProperty(property)!.GetValue(target)!;
    /// <summary>
    /// 验证 Set 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 SetValue、GetProperty、GetType；具体条件和先后次序见方法体。
    /// </summary>
    private static void Set(object target, string property, object value) => target.GetType().GetProperty(property)!.SetValue(target, value);
    /// <summary>
    /// 验证 Render 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Measure、Arrange、UpdateLayout、Render；具体条件和先后次序见方法体。
    /// </summary>
    private static void Render(FrameworkElement view)
    {
        view.Measure(new Size(1000, 700)); view.Arrange(new Rect(0, 0, 1000, 700)); view.UpdateLayout();
        new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32).Render(view);
    }
    /// <summary>
    /// 验证 Descendants 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        { var child = VisualTreeHelper.GetChild(root, index); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    /// <summary>
    /// 验证 PumpUntil 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AddSeconds、done、BeginInvoke、PushFrame、Sleep；具体条件和先后次序见方法体。
    /// </summary>
    private static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!done() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }
        Assert.True(done(), "Query did not update the real bound model.");
    }
    /// <summary>
    /// CapacityReader 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICapacityStatisticsReader&lt;CapacityStatisticsRow&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CapacityReader(CapacityStatisticsRow row) : ICapacityStatisticsReader<CapacityStatisticsRow>
    {
        public int Calls;
        /// <summary>
        /// 验证 Read 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IReadOnlyList<CapacityStatisticsEntry<CapacityStatisticsRow>> Read(DateTime start, DateTime end)
        { Calls++; var ok = new int[24]; var ng = new int[24]; ok[0] = row.OKCOunt1; ng[23] = row.NGCount24; return [new(row, row.Time, ok, ng)]; }
    }
    /// <summary>
    /// LogReader 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILogHistoryReader&lt;LogInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class LogReader(LogInfo[] rows) : ILogHistoryReader<LogInfo>
    {
        public int Calls;
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IEnumerable<LogInfo>> ReadAsync(DateTime start, DateTime end) { Calls++; return Task.FromResult<IEnumerable<LogInfo>>(rows); }
    }
    /// <summary>
    /// DataRow 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ISQLDataBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class DataRow : ISQLDataBase
    {
        public string ID { get; set; } = "";
        public DateTime Time { get; set; }
    }
    /// <summary>
    /// TableReader 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ITableQueryReader&lt;DataRow&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TableReader : ITableQueryReader<DataRow>
    {
        public int Calls;
        public DataRow[] Rows = [new() { ID = "one" }, new() { ID = "two" }];
        /// <summary>
        /// 验证 Read 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IEnumerable<DataRow> Read(DateTime start, DateTime end, string? id) { Calls++; return Rows; }
    }
    /// <summary>
    /// SemiReader 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILogHistoryReader&lt;string&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class SemiReader : ILogHistoryReader<string>
    {
        public int Calls;
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IEnumerable<string>> ReadAsync(DateTime start, DateTime end) { Calls++; return Task.FromResult<IEnumerable<string>>(["SEMI,1", "", "SEMI,1"]); }
    }

    /// <summary>
    /// SqlProbe 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：DispatchProxy。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class SqlProbe<T> : DispatchProxy
    {
        public List<T> Rows { get; } = [];
        /// <summary>
        /// 验证 Invoke 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToList、Where、Compile、Add、CreateInstance；具体条件和先后次序见方法体。
        /// </summary>
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_SQLParam") return new SQLParam { Name = "S3-probe" };
            if (method.Name == "Query") return args is { Length: 1 } && args[0] is Expression<Func<T, bool>> predicate
                ? Rows.Where(predicate.Compile()).ToList() : Rows.ToList();
            if (method.Name == "Add" && args![0] is T row) Rows.Add(row);
            else if (method.Name != "Update") throw new NotSupportedException(method.Name);
            return method.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
