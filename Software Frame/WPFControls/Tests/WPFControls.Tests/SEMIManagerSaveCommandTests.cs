// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SEMIManagerSaveCommandTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.ProgramControl.Services.Log、WPFControls.ProgramControl.ViewModels.Log、SecsGem.Core、DryIoc、Prism.DryIoc、Prism.Ioc、System.IO、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using SecsGem.Core;
using WPFControls.ProgramControl.Services.Log;
using WPFControls.ProgramControl.ViewModels.Log;
using Xunit;

namespace WPFControls.Tests;

/// <summary>
/// T12c：SEMI 配置页（SECS ReportInfo / EventInfo）的保存链路。
///
/// <para>
/// 背景：页面上的两个 <c>ConfigManagerView</c> **都没有**设置 <c>FilePath</c> / <c>FileName</c>
/// （它们绑的是内存里的 SECS 表，不是磁盘文件），而控件的内置 <c>SaveData</c> 开头就是
/// <c>if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(FileName)) { MessageBox.Show(...); return; }</c>
/// —— 早退分支在 <c>SaveCallback?.Invoke()</c> **之前**。于是点"保存"只会弹一个
/// "请检查文件路径(FilePath)和文件名称(FileName)" 的框，<c>ReportSaveCommand</c> /
/// <c>EventSaveCommand</c> **一次都不会被调用**：两个按钮形同虚设，SECS 报表 / 事件表
/// 永远无法从本页落盘。
/// </para>
///
/// <para>
/// 本套件钉住改绑 <c>SaveCommand</c>（T10 seam）之后的行为：命令真的调到
/// <c>ISecsGemConfiguration.SaveReports / SaveEvents</c>、失败时命令不抛（不崩进程）且状态行
/// 不谎报成功、加载失败也不把界面带崩（<c>LoadCommand</c> 由 XAML 的 <c>Loaded</c> 触发器调用）。
/// </para>
///
/// <para>
    /// M02 之后本页存的是<strong>页面上那张表</strong>：契约加了
    /// <c>SaveReports(IEnumerable&lt;SecsGemReportDefinition&gt;)</c> 重载，
    /// <c>SEMIManagerService</c> 把 <c>Model.Reports</c> 原样传下去。
    /// 此前调的是无参 <c>SaveReports()</c> —— 它序列化 <c>SecsGemRuntime</c> 自己那张表，
    /// 与本页显示的内容毫无关系，状态行那句"已保存"是假的。
    /// </para>
///
/// <para>
/// 不过表格目前仍是<strong>只读</strong>（XAML 两个 <c>ConfigManagerView</c> 都是
/// <c>IsReadOnly="True"</c>，<c>Add</c>/<c>Delete</c> 都 <c>Collapsed</c>），
/// 所以契约虽然通了，用户还改不了东西 —— 开启编辑是另一件事，见下两条用例。
/// </para>
/// </summary>
public sealed class SEMIManagerSaveCommandTests
{
    // ============================================================ 保存：命令真的生效

    /// <summary>
    /// ReportInfo 的保存命令必须真的调到 <c>SaveReports</c>，且不误触 <c>SaveEvents</c>。
    /// 改造前这条链路根本走不到（内置 <c>SaveData</c> 早退）。
    /// </summary>
    [Fact]
    public void ReportSaveCommand_InvokesSaveReportsAndReportsSuccess()
    {
        var configuration = new RecordingConfiguration();
        var viewModel = CreateViewModel(configuration);

        Assert.Equal(string.Empty, viewModel.StatusText);
        viewModel.LoadCommand.Execute(null);   // 页面 Loaded 会先加载；未加载的表拒绝保存
        viewModel.ReportSaveCommand.Execute(null);

        Assert.Equal(1, configuration.SaveReportsCalls);
        Assert.Equal(0, configuration.SaveEventsCalls);
        Assert.StartsWith("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("ReportInfo", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>EventInfo 的保存命令必须真的调到 <c>SaveEvents</c>，且不误触 <c>SaveReports</c>。</summary>
    [Fact]
    public void EventSaveCommand_InvokesSaveEventsAndReportsSuccess()
    {
        var configuration = new RecordingConfiguration();
        var viewModel = CreateViewModel(configuration);
        viewModel.LoadCommand.Execute(null);   // 同上：先加载再保存
        viewModel.EventSaveCommand.Execute(null);

        Assert.Equal(0, configuration.SaveReportsCalls);
        Assert.Equal(1, configuration.SaveEventsCalls);
        Assert.StartsWith("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("EventInfo", viewModel.StatusText, StringComparison.Ordinal);
    }

    // ============================================================ M02：存的是页面上那张表

    /// <summary>
    /// 保存必须把页面上那一行的<strong>内容</strong>交给契约，而不只是喊一声"保存过"。
    /// 改造前 <c>ReportSave()</c> 调无参 <c>SaveReports()</c>，页面内容全程没参与 ——
    /// 这正是"表格编辑永远存不下去"的根因。
    /// </summary>
    [Fact]
    public void ReportSaveCommand_PassesTheRowsShownOnTheGrid()
    {
        var configuration = new RecordingConfiguration
        {
            Reports = [new SecsGemReportDefinition("7", "原名", "1,2")],
        };
        var viewModel = CreateViewModel(configuration);
        viewModel.LoadCommand.Execute(null);

        // record 不可变，只能整条替换 —— 这也是 XAML 现在 IsReadOnly=True 的原因之一
        viewModel.Datas.Reports[0] = viewModel.Datas.Reports[0] with { Name = "改过的名" };

        viewModel.ReportSaveCommand.Execute(null);

        var saved = Assert.Single(configuration.SavedReports!);
        Assert.Equal("7", saved.Id);
        Assert.Equal("改过的名", saved.Name);
        Assert.Equal("1,2", saved.VariableIds);
    }

    /// <summary>事件表同理。</summary>
    [Fact]
    public void EventSaveCommand_PassesTheRowsShownOnTheGrid()
    {
        var configuration = new RecordingConfiguration
        {
            Events = [new SecsGemEventDefinition("9", "原名", "7", true)],
        };
        var viewModel = CreateViewModel(configuration);
        viewModel.LoadCommand.Execute(null);
        viewModel.Datas.Events[0] = viewModel.Datas.Events[0] with { Enabled = false };

        viewModel.EventSaveCommand.Execute(null);

        var saved = Assert.Single(configuration.SavedEvents!);
        Assert.Equal("9", saved.Id);
        Assert.False(saved.Enabled);
    }

    /// <summary>
    /// 没加载过就保存必须拒绝，而不是拿一张空表去覆盖磁盘上的 <c>ReportID.csv</c>。
    /// 静默截断配置文件属于不可逆的数据丢失，比抛异常严重得多。
    /// </summary>
    [Fact]
    public void SaveCommands_RefuseToWriteATableThatWasNeverLoaded()
    {
        var configuration = new RecordingConfiguration
        {
            Reports = [new SecsGemReportDefinition("7", "A", "1")],
            Events = [new SecsGemEventDefinition("9", "B", "7", true)],
        };
        var viewModel = CreateViewModel(configuration);   // 故意不执行 LoadCommand

        viewModel.ReportSaveCommand.Execute(null);
        viewModel.EventSaveCommand.Execute(null);

        Assert.Equal(0, configuration.SaveReportsCalls);
        Assert.Equal(0, configuration.SaveEventsCalls);
        Assert.DoesNotContain("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("尚未加载", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 三个命令都必须是 <see cref="ICommand"/>，不再是「像命令其实是 <c>Action</c>」的属性
    /// （<c>Action</c> 才能绑 <c>SaveCallback</c>，而 <c>SaveCallback</c> 这一页走不通）。
    /// </summary>
    [Fact]
    public void Commands_AreICommandNotAction()
    {
        Assert.Equal(typeof(ICommand),
            typeof(SEMIManagerViewModel).GetProperty(nameof(SEMIManagerViewModel.LoadCommand))!.PropertyType);
        Assert.Equal(typeof(ICommand),
            typeof(SEMIManagerViewModel).GetProperty(nameof(SEMIManagerViewModel.ReportSaveCommand))!.PropertyType);
        Assert.Equal(typeof(ICommand),
            typeof(SEMIManagerViewModel).GetProperty(nameof(SEMIManagerViewModel.EventSaveCommand))!.PropertyType);
    }

    // ============================================================ 保存：失败不崩、不谎报

    /// <summary>
    /// 保存 ReportInfo 失败（CSV 被 Excel 占住 / 目录不可写）：命令**不抛**
    /// （抛出去会冒到 Dispatcher 把界面带崩）、状态行写明失败、**不出现"已保存"**。
    /// </summary>
    [Fact]
    public void ReportSaveCommand_WhenSaveFails_DoesNotThrowAndReportsFailure()
    {
        var configuration = new RecordingConfiguration
        {
            SaveReportsFailure = new IOException("ReportID.csv 正被另一个进程占用"),
        };
        var viewModel = CreateViewModel(configuration);
        viewModel.LoadCommand.Execute(null);   // 同上：先加载再保存

        Assert.Null(Record.Exception(() => viewModel.ReportSaveCommand.Execute(null)));

        Assert.StartsWith("保存失败", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("ReportID.csv 正被另一个进程占用", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(1, configuration.SaveReportsCalls);   // 确实尝试过
    }

    /// <summary>保存 EventInfo 失败同上。</summary>
    [Fact]
    public void EventSaveCommand_WhenSaveFails_DoesNotThrowAndReportsFailure()
    {
        var configuration = new RecordingConfiguration
        {
            SaveEventsFailure = new UnauthorizedAccessException("CEID.csv 只读"),
        };
        var viewModel = CreateViewModel(configuration);
        viewModel.LoadCommand.Execute(null);   // 同上：先加载再保存

        Assert.Null(Record.Exception(() => viewModel.EventSaveCommand.Execute(null)));

        Assert.StartsWith("保存失败", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(1, configuration.SaveEventsCalls);
    }

    // ============================================================ 加载

    /// <summary>加载命令把两张表都灌进 Model。</summary>
    [Fact]
    public void LoadCommand_LoadsBothTablesIntoTheModel()
    {
        var configuration = new RecordingConfiguration
        {
            Reports = [new SecsGemReportDefinition("1", "R1", "1001,1002")],
            Events = [new SecsGemEventDefinition("11", "E1", "1", true)],
        };
        var viewModel = CreateViewModel(configuration);

        viewModel.LoadCommand.Execute(null);

        Assert.Equal(new[] { "1" }, viewModel.Datas.Reports.Select(report => report.Id));
        Assert.Equal(new[] { "11" }, viewModel.Datas.Events.Select(@event => @event.Id));
        Assert.Equal(string.Empty, viewModel.StatusText);
    }

    /// <summary>
    /// 加载失败**不能崩界面**：<c>LoadCommand</c> 由 XAML 的 <c>Loaded</c> 触发器调用，
    /// 抛出去等于「一进这一页就崩」。配置文件缺失 / 被占住都是预期内场景。
    /// </summary>
    [Fact]
    public void LoadCommand_WhenLoadFails_DoesNotThrowAndReportsFailure()
    {
        var configuration = new RecordingConfiguration
        {
            LoadFailure = new IOException("ReportID.csv 不存在"),
        };
        var viewModel = CreateViewModel(configuration);

        Assert.Null(Record.Exception(() => viewModel.LoadCommand.Execute(null)));

        Assert.StartsWith("加载 SECS 配置失败", viewModel.StatusText, StringComparison.Ordinal);
    }

    // ============================================================ 工具

    /// <summary>
    /// 搭一个最小 DryIoc 容器：<c>SEMIManagerService</c> 的构造函数只要一个
    /// <see cref="ISecsGemConfiguration"/>，<c>SEMIManagerViewModel</c> 再从这个容器解析它。
    /// 与 T12b 不同，这里不需要 SECS 运行时桩，也不需要 Dispatcher（本页没有
    /// <c>DataGridService</c> / <c>DispatcherTimer</c>）。
    /// </summary>
    private static SEMIManagerViewModel CreateViewModel(ISecsGemConfiguration configuration)
    {
        var container = new Container();
        container.RegisterInstance<ISecsGemConfiguration>(configuration);
        container.Register<SEMIManagerService>(Reuse.Singleton);

        // ⚠️ 别在这里再注册 IContainerProvider：DryIocContainerExtension 构造函数自己就注册了，
        // 重复注册会让解析抛 Error.ExpectedSingleDefaultFactory。
        return new SEMIManagerViewModel(new DryIocContainerExtension(container));
    }

    /// <summary>
    /// <see cref="ISecsGemConfiguration"/> 的记录型桩：断言「有没有被要求保存、保存哪一张」，
    /// 并可注入加载 / 保存失败。
    /// </summary>
    private sealed class RecordingConfiguration : ISecsGemConfiguration
    {
        public List<SecsGemReportDefinition> Reports { get; set; } = [];

        public List<SecsGemEventDefinition> Events { get; set; } = [];

        public int SaveReportsCalls { get; private set; }

        public int SaveEventsCalls { get; private set; }

        /// <summary>最近一次经重载传入的报表定义；页面上的行必须原样到达契约，不能被换掉。</summary>
        public List<SecsGemReportDefinition>? SavedReports { get; private set; }

        /// <summary>最近一次经重载传入的事件定义。</summary>
        public List<SecsGemEventDefinition>? SavedEvents { get; private set; }

        public Exception? LoadFailure { get; set; }

        public Exception? SaveReportsFailure { get; set; }

        public Exception? SaveEventsFailure { get; set; }

        public IReadOnlyList<SecsGemReportDefinition> LoadReports()
        {
            if (LoadFailure is not null) throw LoadFailure;
            return Reports;
        }

        public IReadOnlyList<SecsGemEventDefinition> LoadEvents()
        {
            if (LoadFailure is not null) throw LoadFailure;
            return Events;
        }

        public void SaveReports()
        {
            SaveReportsCalls++;
            if (SaveReportsFailure is not null) throw SaveReportsFailure;
        }

        public void SaveEvents()
        {
            SaveEventsCalls++;
            if (SaveEventsFailure is not null) throw SaveEventsFailure;
        }

        public void SaveReports(IEnumerable<SecsGemReportDefinition> reports)
        {
            SavedReports = reports.ToList();
            SaveReports();
        }

        public void SaveEvents(IEnumerable<SecsGemEventDefinition> events)
        {
            SavedEvents = events.ToList();
            SaveEvents();
        }
    }
}
