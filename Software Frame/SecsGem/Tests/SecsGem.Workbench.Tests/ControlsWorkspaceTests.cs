// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ControlsWorkspaceTests、ObservedRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows.Controls、SecsGem.Runtime.Variable、Extension.UI.Theme、Prism.Ioc、SecsGem.Controls.Services、SecsGem.Controls.Terminal。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Secs4Net;
using SecsGem.Runtime.Variable;
using Extension.UI.Theme;
using Prism.Ioc;
using SecsGem.Controls.Models;
using SecsGem.Controls.Services;
using SecsGem.Controls.Terminal;
using SecsGem.Controls.ViewModels;
using SecsGem.Controls.Views;
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SecsGem.Workbench.Tests;

/// <summary>
/// ControlsWorkspaceTests 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class ControlsWorkspaceTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 测试相关入口 MultiplePages_ShareState_DisposingOneLeavesWorkspaceUsable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、nameof、NotNull、NotSame；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MultiplePages_ShareState_DisposingOneLeavesWorkspaceUsable()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var workspace = container.Resolve<SecsWorkspace>();
            var first = container.Resolve<object>(nameof(SecsView)) as SecsView;
            var second = container.Resolve<object>(nameof(SecsView)) as SecsView;
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            var vm = Assert.IsType<SecsViewModel>(first.DataContext);
            var other = Assert.IsType<SecsViewModel>(second.DataContext);
            Assert.Same(workspace.Model, vm.Model);
            Assert.Same(vm.Model, other.Model);
            var original = vm.Sml;
            try
            {
                vm.Sml = "'S1F3' W";
                Assert.Equal(vm.Sml, other.Sml);
                first.Dispose();
                using var lease = other.Subscribe();
                other.RefreshState();
                Assert.Equal(workspace.Model.ConnectionStatus, other.ConnectionStatus);
            }
            finally { workspace.Model.Sml = original; first.Dispose(); second.Dispose(); }
        });
    }

    /// <summary>
    /// 测试相关入口 WorkspaceDispose_DoesNotDisposeInjectedRuntime；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Subscribe、Dispose、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WorkspaceDispose_DoesNotDisposeInjectedRuntime()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            using var runtime = new ObservedRuntime(container);
            var workspace = new SecsWorkspace(runtime, container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var lease = workspace.Subscribe();
            workspace.Dispose();
            workspace.Dispose();
            Assert.Equal(0, runtime.DisposeCount);
            Assert.Throws<ObjectDisposedException>(() => workspace.Subscribe());
        });
    }

    /// <summary>
    /// 测试相关入口 CommandsAndRealButtons_FollowConnectionParametersAndSelection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Create、Measure、Arrange；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CommandsAndRealButtons_FollowConnectionParametersAndSelection()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            using var runtime = new ObservedRuntime(container);
            using var workspace = new SecsWorkspace(runtime, container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var vm = new SecsViewModel(workspace);
            using var page = SecsView.Create(vm);
            page.Measure(new Size(1400, 900));
            page.Arrange(new Rect(0, 0, 1400, 900));
            page.UpdateLayout();
            Button ButtonFor(string caption) => Descendants(page).OfType<Button>().Single(b => Equals(b.Content, caption));
            void Flush() => page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.True(ButtonFor("连接").IsEnabled);
            Assert.False(ButtonFor("断开").IsEnabled);
            Assert.False(ButtonFor("建立通信 S1F13").IsEnabled);
            Assert.False(ButtonFor("发送").IsEnabled);
            vm.PortText = "0";
            Flush();
            Assert.False(ButtonFor("连接").IsEnabled);
            vm.PortText = "5000";
            // DeviceID 只在被动模式下参与通信。主动模式填 32768（原 15 位上限之外）
            // 不该挡住连接；被动模式才是真的越界。
            vm.DeviceIdText = "32768";
            Flush();
            Assert.True(ButtonFor("连接").IsEnabled);
            vm.ConnectionModeIndex = 1;
            Flush();
            Assert.False(ButtonFor("连接").IsEnabled);
            vm.DeviceIdText = "0";
            vm.ConnectionModeIndex = 0;
            Flush();
            Assert.True(ButtonFor("连接").IsEnabled);
            workspace.Model.IsConnected = true;
            Flush();
            Assert.False(ButtonFor("连接").IsEnabled);
            Assert.True(ButtonFor("断开").IsEnabled);
            Assert.True(ButtonFor("建立通信 S1F13").IsEnabled);
            Assert.True(ButtonFor("发送").IsEnabled);
            vm.Sml = " ";
            Flush();
            Assert.False(ButtonFor("发送").IsEnabled);
            Assert.False(vm.ReportEventCommand.CanExecute(null));
            vm.SelectedEvent = new CEIDInfo { ID = "1", Name = "Test" };
            vm.SelectedAlarm = new AlarmInfo { ID = "1", Description = "Test" };
            Assert.True(vm.ReportEventCommand.CanExecute(null));
            Assert.True(vm.RaiseAlarmCommand.CanExecute(null));
            Assert.True(vm.ResetAlarmCommand.CanExecute(null));
            workspace.Model.IsConnected = false;
            Flush();
            Assert.False(ButtonFor("断开").IsEnabled);
            Assert.False(vm.ReportEventCommand.CanExecute(null));
            Assert.False(vm.RaiseAlarmCommand.CanExecute(null));
            Assert.False(vm.SetVariableCommand.CanExecute(null));
            vm.SelectedVariable = new SecsGem.Controls.Models.VariableRow("SVID", new VIDInfo { ID = "1", Name = "Test" });
            Assert.True(vm.SetVariableCommand.CanExecute(null)); // 本地变量编辑不要求连接。
            Assert.True(vm.LoadSampleCommand.CanExecute(null));
        });
    }

    /// <summary>
    /// 测试相关入口 DisposedBorrower_DisablesOnlyOwnCommands_ClosedWorkspaceDisablesAll；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Add、Dispose、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DisposedBorrower_DisablesOnlyOwnCommands_ClosedWorkspaceDisablesAll()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            using var runtime = new ObservedRuntime(container);
            using var workspace = new SecsWorkspace(runtime, container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var first = new SecsViewModel(workspace);
            using var second = new SecsViewModel(workspace);
            workspace.Messages.Add(new LogRow("00:00:00.000", "测试", "Keep"));
            first.Dispose();
            Assert.Throws<ObjectDisposedException>(() => first.Subscribe());
            Assert.False(first.ConnectCommand.CanExecute(null));
            Assert.False(first.ClearLogCommand.CanExecute(null));
            first.ClearLogCommand.Execute(null);
            Assert.Contains(workspace.Messages, m => m.Text == "Keep");
            Assert.True(second.ClearLogCommand.CanExecute(null));
            Assert.True(second.ConnectCommand.CanExecute(null));
            workspace.Dispose();
            Assert.False(second.ConnectCommand.CanExecute(null));
            Assert.False(second.ClearLogCommand.CanExecute(null));
            workspace.ClearLog();
            Assert.Contains(workspace.Messages, m => m.Text == "Keep");
            Assert.Equal(0, runtime.DisposeCount);
        });
    }

    /// <summary>
    /// 列表行只显示首行摘要（行高等高是虚拟化的前提），全文在详情框里展开。
    /// </summary>
    [Fact]
    public void LogRow_Summary_Is_The_First_Line_Only()
    {
        var row = new LogRow("16:42:00.123", "发送", "S6F11 --> [0x07EBFFA0] 'S6F11'W\n  <L [3]>\n    <A [1] '2'>");
        Assert.Equal("S6F11 --> [0x07EBFFA0] 'S6F11'W", row.Summary);
        Assert.Contains("\n", row.Text);   // 全文保留换行，详情框负责展示
    }

    /// <summary>
    /// 测试相关入口 AddVariable_PersistsNewVid_AndRejectsDuplicates；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Create、Assert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AddVariable_PersistsNewVid_AndRejectsDuplicates()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var catalog = container.Resolve<VIDManager>();
            var root = Path.Combine(Path.GetTempPath(), "SecsGemControlsTests", Guid.NewGuid().ToString("N"));
            var originalPath = catalog.SVID.FilePath;
            catalog.SVID.FilePath = root;
            var name = "NewVid_" + Guid.NewGuid().ToString("N");
            try
            {
                using var runtime = new ObservedRuntime(container);
                using var workspace = new SecsWorkspace(runtime, catalog, Dispatcher.CurrentDispatcher);
                using var vm = new SecsViewModel(workspace);
                Assert.False(vm.AddVariableCommand.CanExecute(null));
                vm.NewVariableId = "9001";
                vm.NewVariableName = name;
                Assert.True(vm.AddVariableCommand.CanExecute(null));
                vm.AddVariableCommand.Execute(null);

                var created = catalog.GetVIDByName(name);
                Assert.NotNull(created);
                Assert.Equal("9001", created.ID);
                Assert.Equal(SecsFormat.ASCII, created.DataType);
                Assert.Contains(catalog.SVID.Datas, x => x.ID == "9001");
                Assert.Contains(workspace.Model.Variables, x => x.Name == name);
                Assert.True(File.Exists(Path.Combine(root, "SVID.csv")));

                vm.NewVariableId = "9001";
                vm.NewVariableName = name + "_dup";
                vm.AddVariableCommand.Execute(null);
                Assert.Equal(1, catalog.VIDList.Count(x => x.ID == "9001")); // 重复 ID 被拒。
            }
            finally
            {
                foreach (var stale in catalog.SVID.Datas.Where(x => x.Name.StartsWith("NewVid_")).ToArray()) catalog.SVID.Datas.Remove(stale);
                foreach (var stale in catalog.VIDList.Where(x => x.Name.StartsWith("NewVid_")).ToArray()) catalog.VIDList.Remove(stale);
                foreach (var id in catalog.VIDDic_ID.Keys.Where(k => k == "9001").ToArray()) catalog.VIDDic_ID.Remove(id);
                foreach (var key in catalog.VIDDic_Name.Keys.Where(k => k.StartsWith("NewVid_")).ToArray()) catalog.VIDDic_Name.Remove(key);
                catalog.SVID.FilePath = originalPath;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    /// <summary>
    /// 测试相关入口 AddReportAndAddEvent_PersistRows_AndRejectUnknownReferences；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Create、Assert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AddReportAndAddEvent_PersistRows_AndRejectUnknownReferences()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var catalog = container.Resolve<VIDManager>();
            var root = Path.Combine(Path.GetTempPath(), "SecsGemControlsTests", Guid.NewGuid().ToString("N"));
            var originalReportPath = catalog.ReportID.FilePath;
            var originalEventPath = catalog.CEID.FilePath;
            var vidId = "9" + Guid.NewGuid().ToString("N")[..6];
            catalog.ReportID.FilePath = root;
            catalog.CEID.FilePath = root;
            catalog.ReportID.Datas.Clear();
            catalog.CEID.Datas.Clear();
            var seedVid = new VIDInfo { ID = vidId, Name = "SeedVid", Value = "", DataType = SecsFormat.ASCII };
            catalog.SVID.Datas.Add(seedVid);
            catalog.VIDList.Add(seedVid);
            catalog.VIDDic_Name[seedVid.Name] = seedVid;
            catalog.VIDDic_ID[vidId] = seedVid;
            try
            {
                using var runtime = new ObservedRuntime(container);
                using var workspace = new SecsWorkspace(runtime, catalog, Dispatcher.CurrentDispatcher);
                using var vm = new SecsViewModel(workspace);

                vm.NewReportId = "77";
                vm.NewReportName = "NewReport";
                vm.NewReportVids = "does-not-exist";
                Assert.True(vm.AddReportCommand.CanExecute(null));
                vm.AddReportCommand.Execute(null);
                Assert.Empty(catalog.ReportID.Datas); // 未知 VID 被拦下，不落盘。

                vm.NewReportVids = vidId;
                vm.AddReportCommand.Execute(null);
                var report = Assert.Single(catalog.ReportID.Datas);
                Assert.Equal("77", report.ID);
                Assert.Equal("NewReport", report.Name);
                Assert.Contains(workspace.Model.Reports, x => x.ID == "77");
                Assert.True(File.Exists(Path.Combine(root, "ReportID.csv")));

                vm.NewReportId = "77";
                vm.AddReportCommand.Execute(null);
                Assert.Single(catalog.ReportID.Datas); // 重复 Report ID 被拒。

                vm.NewEventId = "88";
                vm.NewEventName = "NewEvent";
                vm.NewEventReports = "not-a-report";
                vm.AddEventCommand.Execute(null);
                Assert.Empty(catalog.CEID.Datas);

                vm.NewEventReports = "77";
                vm.AddEventCommand.Execute(null);
                var evt = Assert.Single(catalog.CEID.Datas);
                Assert.Equal("88", evt.ID);
                Assert.Equal("77", evt.ReportID_Collection);
                Assert.Contains(workspace.Model.Events, x => x.ID == "88");
                Assert.True(File.Exists(Path.Combine(root, "CEID.csv")));
            }
            finally
            {
                catalog.ReportID.Datas.Clear();
                catalog.CEID.Datas.Clear();
                catalog.SVID.Datas.Remove(seedVid);
                catalog.VIDList.Remove(seedVid);
                catalog.VIDDic_Name.Remove(seedVid.Name);
                catalog.VIDDic_ID.Remove(vidId);
                catalog.ReportID.FilePath = originalReportPath;
                catalog.CEID.FilePath = originalEventPath;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    /// <summary>
    /// 删除变量/报表/事件：行消失且落盘；被引用的不让删（否则 ReportID/CEID.Update 遇到悬空引用直接抛，
    /// 整条 S6F11 组包失败）。删完事件后报表可删，删完报表后变量可删，链条是通的。
    /// </summary>
    [Fact]
    public void DeleteVariable_Report_Event_RemoveRows_AndBlockReferencedOnes()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var catalog = container.Resolve<VIDManager>();
            var root = Path.Combine(Path.GetTempPath(), "SecsGemControlsTests", Guid.NewGuid().ToString("N"));
            var originalSvidPath = catalog.SVID.FilePath;
            var originalReportPath = catalog.ReportID.FilePath;
            var originalEventPath = catalog.CEID.FilePath;
            var tag = Guid.NewGuid().ToString("N")[..6];
            var freeVidId = "8" + tag[..5];
            var usedVidId = "7" + tag[..5];
            catalog.SVID.FilePath = root;
            catalog.ReportID.FilePath = root;
            catalog.CEID.FilePath = root;
            catalog.SVID.Datas.Clear();
            catalog.ReportID.Datas.Clear();
            catalog.CEID.Datas.Clear();
            var freeVid = new VIDInfo { ID = freeVidId, Name = "FreeVid_" + tag, Value = "", DataType = SecsFormat.ASCII, Category = "SVID" };
            var usedVid = new VIDInfo { ID = usedVidId, Name = "UsedVid_" + tag, Value = "", DataType = SecsFormat.ASCII, Category = "SVID" };
            foreach (var v in new[] { freeVid, usedVid })
            {
                catalog.SVID.Datas.Add(v);
                catalog.VIDList.Add(v);
                catalog.VIDDic_Name[v.Name] = v;
                catalog.VIDDic_ID[v.ID] = v;
            }
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "71", Name = "UsedReport", VID_Collection = usedVidId });
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "72", Name = "FreeReport", VID_Collection = freeVidId });
            catalog.CEID.Datas.Add(new CEIDInfo { ID = "81", Name = "UsedEvent", ReportID_Collection = "71", Enable = true });
            try
            {
                using var runtime = new ObservedRuntime(container);
                using var workspace = new SecsWorkspace(runtime, catalog, Dispatcher.CurrentDispatcher);
                using var vm = new SecsViewModel(workspace);

                // 被报表引用的变量删不掉
                vm.SelectedVariable = workspace.Model.Variables.Single(x => x.Id == usedVidId);
                Assert.True(vm.DeleteVariableCommand.CanExecute(null));
                vm.DeleteVariableCommand.Execute(null);
                Assert.Contains(catalog.SVID.Datas, x => x.ID == usedVidId);
                Assert.Contains("引用", workspace.Model.Messages.Last().Text);

                // 被事件引用的报表删不掉
                vm.SelectedReport = workspace.Model.Reports.Single(x => x.ID == "71");
                Assert.True(vm.DeleteReportCommand.CanExecute(null));
                vm.DeleteReportCommand.Execute(null);
                Assert.Contains(catalog.ReportID.Datas, x => x.ID == "71");

                // 先删事件，再删报表，最后删变量，链条打通
                vm.SelectedEvent = workspace.Model.Events.Single(x => x.ID == "81");
                vm.DeleteEventCommand.Execute(null);
                Assert.DoesNotContain(catalog.CEID.Datas, x => x.ID == "81");

                vm.SelectedReport = workspace.Model.Reports.Single(x => x.ID == "71");
                vm.DeleteReportCommand.Execute(null);
                Assert.DoesNotContain(catalog.ReportID.Datas, x => x.ID == "71");

                vm.SelectedVariable = workspace.Model.Variables.Single(x => x.Id == usedVidId);
                vm.DeleteVariableCommand.Execute(null);
                Assert.DoesNotContain(catalog.SVID.Datas, x => x.ID == usedVidId);
                Assert.DoesNotContain(catalog.VIDList, x => x.ID == usedVidId);
                Assert.False(catalog.VIDDic_ID.ContainsKey(usedVidId));

                // 落盘验证：重读文件，删掉的不在，留下的在
                var svidCsv = File.ReadAllText(Path.Combine(root, "SVID.csv"));
                Assert.DoesNotContain(usedVidId, svidCsv);
                Assert.Contains(freeVidId, svidCsv);
                var ceidCsv = File.ReadAllText(Path.Combine(root, "CEID.csv"));
                Assert.DoesNotContain("UsedEvent", ceidCsv);
            }
            finally
            {
                foreach (var v in new[] { freeVid, usedVid })
                {
                    catalog.SVID.Datas.Remove(v);
                    catalog.VIDList.Remove(v);
                    catalog.VIDDic_Name.Remove(v.Name);
                    catalog.VIDDic_ID.Remove(v.ID);
                }
                catalog.ReportID.Datas.Clear();
                catalog.CEID.Datas.Clear();
                catalog.SVID.FilePath = originalSvidPath;
                catalog.ReportID.FilePath = originalReportPath;
                catalog.CEID.FilePath = originalEventPath;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    /// <summary>
    /// 写入变量值即时落盘：原来值只在内存，重启就丢，是全工作台唯一不持久化的编辑。
    /// 现在 SetVariable 内部直接存当类 CSV，不需要额外的保存按钮。
    /// </summary>
    [Fact]
    public void SetVariable_Persists_Value_Immediately()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var catalog = container.Resolve<VIDManager>();
            var root = Path.Combine(Path.GetTempPath(), "SecsGemControlsTests", Guid.NewGuid().ToString("N"));
            var originalSvidPath = catalog.SVID.FilePath;
            var tag = Guid.NewGuid().ToString("N")[..6];
            catalog.SVID.FilePath = root;
            catalog.SVID.Datas.Clear();
            var seed = new VIDInfo { ID = "8" + tag[..5], Name = "SaveMe_" + tag, Value = "before", DataType = SecsFormat.ASCII, Category = "SVID" };
            catalog.SVID.Datas.Add(seed);
            catalog.VIDList.Add(seed);
            catalog.VIDDic_Name[seed.Name] = seed;
            catalog.VIDDic_ID[seed.ID] = seed;
            try
            {
                using var runtime = new ObservedRuntime(container);
                using var workspace = new SecsWorkspace(runtime, catalog, Dispatcher.CurrentDispatcher);
                using var vm = new SecsViewModel(workspace);

                vm.SelectedVariable = workspace.Model.Variables.Single(x => x.Id == seed.ID);
                vm.VariableValue = "after";
                vm.SetVariableCommand.Execute(null);

                // 不点任何保存按钮，CSV 里直接有值
                var csv = File.ReadAllText(Path.Combine(root, "SVID.csv"));
                Assert.Contains("after", csv);
                Assert.Contains(seed.ID, csv);
            }
            finally
            {
                catalog.SVID.Datas.Remove(seed);
                catalog.VIDList.Remove(seed);
                catalog.VIDDic_Name.Remove(seed.Name);
                catalog.VIDDic_ID.Remove(seed.ID);
                catalog.SVID.FilePath = originalSvidPath;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    /// <summary>
    /// Host 改配置类指令（S2F33/S2F35/S2F37）触发后，界面快照必须重刷。
    /// <para>
    /// 回归：三个 handler 只改运行时目录的内存表，从不通知界面，改完页面上还是旧的。
    /// 引擎在每条收到的主消息回包后都按 order 名触发事件，这里直接模拟那一步：
    /// 外部删掉一行 → 触发事件 → 界面快照里消失。不断言 handler 解析本身（引擎级另有覆盖）。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("S2F33")]
    [InlineData("S2F35")]
    [InlineData("S2F37")]
    public void Host_Config_Change_Events_Refresh_The_Catalog_Snapshot(string order)
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var catalog = container.Resolve<VIDManager>();
            var root = Path.Combine(Path.GetTempPath(), "SecsGemControlsTests", Guid.NewGuid().ToString("N"));
            var originalReportPath = catalog.ReportID.FilePath;
            var originalEventPath = catalog.CEID.FilePath;
            catalog.ReportID.FilePath = root;
            catalog.CEID.FilePath = root;
            catalog.ReportID.Datas.Clear();
            catalog.CEID.Datas.Clear();
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "77", Name = "Gone", VID_Collection = "1" });
            catalog.CEID.Datas.Add(new CEIDInfo { ID = "88", Name = "Gone", ReportID_Collection = "77", Enable = true });
            try
            {
                using var runtime = new ObservedRuntime(container);
                using var workspace = new SecsWorkspace(runtime, catalog, Dispatcher.CurrentDispatcher);
                using var vm = new SecsViewModel(workspace);
                _ = vm;

                Assert.Contains(workspace.Model.Reports, x => x.ID == "77");
                Assert.Contains(workspace.Model.Events, x => x.ID == "88");

                // 模拟目录在界面之外被改掉（S2F33/S2F35/S2F37 干的就是这个）
                foreach (var stale in catalog.ReportID.Datas.Where(x => x.ID == "77").ToArray())
                    catalog.ReportID.Datas.Remove(stale);
                foreach (var stale in catalog.CEID.Datas.Where(x => x.ID == "88").ToArray())
                    catalog.CEID.Datas.Remove(stale);

                Extension.Core.Concurrency.EventHelper.TriggerEvent(order);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                Assert.DoesNotContain(workspace.Model.Reports, x => x.ID == "77");
                Assert.DoesNotContain(workspace.Model.Events, x => x.ID == "88");
            }
            finally
            {
                catalog.ReportID.Datas.Clear();
                catalog.CEID.Datas.Clear();
                catalog.ReportID.FilePath = originalReportPath;
                catalog.CEID.FilePath = originalEventPath;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    /// <summary>
    /// 事件与报表可以手动改：选中行回填修改框 → 改值 → 保存落盘；引用不合法就地拒绝。
    /// <para>
    /// 回归：表格是 <c>IsReadOnly=True</c>，原来只有"新增/删除"，选中行改不掉 ——
    /// 要改名或调 VID/报表清单只能去改 CSV，或等 Host 用 S2F33/S2F35 推过来。
    /// </para>
    /// </summary>
    [Fact]
    public void Selected_Report_And_Event_Can_Be_Edited_By_Hand()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var catalog = container.Resolve<VIDManager>();
            var root = Path.Combine(Path.GetTempPath(), "SecsGemControlsTests", Guid.NewGuid().ToString("N"));
            var originalSvidPath = catalog.SVID.FilePath;
            var originalReportPath = catalog.ReportID.FilePath;
            var originalEventPath = catalog.CEID.FilePath;
            var tag = Guid.NewGuid().ToString("N")[..6];
            var vidA = "6" + tag[..5];
            var vidB = "5" + tag[..5];
            catalog.SVID.FilePath = root;
            catalog.ReportID.FilePath = root;
            catalog.CEID.FilePath = root;
            catalog.SVID.Datas.Clear();
            catalog.ReportID.Datas.Clear();
            catalog.CEID.Datas.Clear();
            var a = new VIDInfo { ID = vidA, Name = "A_" + tag, Value = "", DataType = SecsFormat.ASCII, Category = "SVID" };
            var b = new VIDInfo { ID = vidB, Name = "B_" + tag, Value = "", DataType = SecsFormat.ASCII, Category = "SVID" };
            foreach (var v in new[] { a, b })
            {
                catalog.SVID.Datas.Add(v);
                catalog.VIDList.Add(v);
                catalog.VIDDic_Name[v.Name] = v;
                catalog.VIDDic_ID[v.ID] = v;
            }
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "70", Name = "R0", VID_Collection = vidA });
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "69", Name = "R9", VID_Collection = vidB });
            catalog.CEID.Datas.Add(new CEIDInfo { ID = "80", Name = "E0", ReportID_Collection = "70", Enable = true });
            try
            {
                using var runtime = new ObservedRuntime(container);
                using var workspace = new SecsWorkspace(runtime, catalog, Dispatcher.CurrentDispatcher);
                using var vm = new SecsViewModel(workspace);

                // 选中 Report：共用输入框要回填现有值，否则用户得整段重打
                // 回归：新命令漏进 RefreshCommands 时 CanExecuteChanged 从不触发，
                // WPF 就一直按初始的 false 把按钮画灰 —— 输入框都回填了，按钮还是点不了。
                var reportRequery = 0;
                var eventRequery = 0;
                vm.SetReportCommand.CanExecuteChanged += (_, _) => reportRequery++;
                vm.SetEventCommand.CanExecuteChanged += (_, _) => eventRequery++;

                vm.SelectedReport = workspace.Model.Reports.Single(x => x.ID == "70");
                Assert.True(reportRequery > 0);
                Assert.True(vm.SetReportCommand.CanExecute(null));
                Assert.Equal("70", vm.Model.NewReportId);
                Assert.Equal("R0", vm.Model.NewReportName);
                Assert.Equal(vidA, vm.Model.NewReportVids);

                vm.Model.NewReportName = "R0_改名";
                vm.Model.NewReportVids = $"{vidB}, {vidA}";   // 换序 + 带空格，顺带验证切分
                Assert.True(vm.SetReportCommand.CanExecute(null));
                vm.SetReportCommand.Execute(null);
                var report = catalog.ReportID.GetDatas().Single(x => x.ID == "70");
                Assert.Equal("R0_改名", report.Name);
                Assert.Equal($"{vidB},{vidA}", report.VID_Collection);

                // 引用不存在的 VID：就地拒绝并点名，不写盘
                vm.Model.NewReportVids = "999999";
                vm.SetReportCommand.Execute(null);
                Assert.Equal($"{vidB},{vidA}", catalog.ReportID.GetDatas().Single(x => x.ID == "70").VID_Collection);
                Assert.Contains("999999", workspace.Model.Messages.Last().Text);

                // 改 ID 要明确拒绝，不能静默当成改了（ID 是键，改=删一个加一个）
                vm.Model.NewReportId = "700";
                vm.Model.NewReportName = "R0_改名2";
                vm.SetReportCommand.Execute(null);
                Assert.Equal("R0_改名", catalog.ReportID.GetDatas().Single(x => x.ID == "70").Name);
                Assert.Contains("ID 不能改", workspace.Model.Messages.Last().Text);

                // 选中 Event：回填 + 改报表清单与使能
                vm.SelectedEvent = workspace.Model.Events.Single(x => x.ID == "80");
                Assert.True(eventRequery > 0);
                Assert.True(vm.SetEventCommand.CanExecute(null));
                Assert.Equal("80", vm.Model.NewEventId);
                Assert.Equal("E0", vm.Model.NewEventName);
                Assert.Equal("70", vm.Model.NewEventReports);
                Assert.True(vm.Model.NewEventEnable);

                vm.Model.NewEventName = "E0_改名";
                vm.Model.NewEventReports = "69,70";
                vm.Model.NewEventEnable = false;
                vm.SetEventCommand.Execute(null);
                var evt = catalog.CEID.GetDatas().Single(x => x.ID == "80");
                Assert.Equal("E0_改名", evt.Name);
                Assert.Equal("69,70", evt.ReportID_Collection);
                Assert.False(evt.Enable);

                // 引用不存在的 Report：同样拒绝
                vm.Model.NewEventReports = "88888";
                vm.SetEventCommand.Execute(null);
                Assert.Equal("69,70", catalog.CEID.GetDatas().Single(x => x.ID == "80").ReportID_Collection);

                // 落盘验证
                var reportCsv = File.ReadAllText(Path.Combine(root, "ReportID.csv"));
                Assert.Contains("R0_改名", reportCsv);
                Assert.Contains($"{vidB},{vidA}", reportCsv);
                var ceidCsv = File.ReadAllText(Path.Combine(root, "CEID.csv"));
                Assert.Contains("E0_改名", ceidCsv);
                Assert.Contains("69,70", ceidCsv);
            }
            finally
            {
                foreach (var v in new[] { a, b })
                {
                    catalog.SVID.Datas.Remove(v);
                    catalog.VIDList.Remove(v);
                    catalog.VIDDic_Name.Remove(v.Name);
                    catalog.VIDDic_ID.Remove(v.ID);
                }
                catalog.ReportID.Datas.Clear();
                catalog.CEID.Datas.Clear();
                catalog.SVID.FilePath = originalSvidPath;
                catalog.ReportID.FilePath = originalReportPath;
                catalog.CEID.FilePath = originalEventPath;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    /// <summary>
    /// ObservedRuntime 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：SecsGemRuntime。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ObservedRuntime(IContainerProvider container) : SecsGemRuntime(container)
    {
        public int DisposeCount { get; private set; }
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
        protected override void Dispose(bool disposing) { DisposeCount++; base.Dispose(disposing); }
    }

    /// <summary>
    /// 测试相关入口 TerminalPrompt_FollowsLightAndDarkForeground；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Show、Initialize、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TerminalPrompt_FollowsLightAndDarkForeground()
    {
        host.Invoke(() =>
        {
            var theme = ContainerLocator.Container.Resolve<ThemeService>();
            var original = (theme.IsDark, theme.Primary, theme.Secondary);
            var prompt = new MessagePrompt("Theme title", "Theme message");
            try
            {
                prompt.Show();
                foreach (var dark in new[] { false, true })
                {
                    theme.Initialize(dark, original.Primary, original.Secondary);
                    prompt.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var expected = Assert.IsAssignableFrom<SolidColorBrush>(prompt.FindResource("MaterialDesign.Brush.Foreground")).Color;
                    var texts = Descendants(prompt).OfType<TextBlock>().Where(t => t.Text is "Theme title" or "Theme message").ToArray();
                    Assert.Equal(2, texts.Length);
                    foreach (var text in texts)
                        Assert.Equal(expected, Assert.IsAssignableFrom<SolidColorBrush>(text.Foreground).Color);
                }
            }
            finally { prompt.Close(); theme.Initialize(original.IsDark, original.Primary, original.Secondary); }
        });
    }

    /// <summary>
    /// 测试相关入口 Descendants；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
