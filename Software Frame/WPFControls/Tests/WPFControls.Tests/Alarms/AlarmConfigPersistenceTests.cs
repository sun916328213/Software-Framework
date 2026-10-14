// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AlarmConfigPersistenceTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services、WPFControls.ProgramControl.ViewModels.Alarm、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Runtime.Excel.EPPlus、WPFControls.Controls.Diagnostics、SecsGem.Core、DryIoc、Prism.DryIoc、Prism.Ioc、System.IO、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using DataManager.Core.Log;
using DataManager.Runtime.Excel.EPPlus;
using DataManager.Runtime.Log;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using SecsGem.Core;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using WPFControls.ProgramControl.ViewModels.Alarm;
using Xunit;

namespace WPFControls.Tests.Alarms;

/// <summary>
/// T12b：报警配置（<c>Config\Alarm.csv</c>）的「落盘 + 加载」归属。
///
/// <para>
/// 背景：改造前报警表的持久化散在两处 —— <c>ConfigManagerView.SaveData</c> 先
/// <c>JsonEx.Save</c> 往 <c>Alarm.csv</c> 写一份 <b>JSON</b>，再在 try/catch <b>之外</b>调
/// <c>SaveCallback</c>（一个 <c>Action</c>，内容是 <c>async () =&gt; { await Service.Save(); }</c>，
/// 即 async-void lambda，VSTHRD101）让服务用 CSV 覆盖回去。三条活的风险：
/// </para>
/// <list type="number">
/// <item>写盘窗口期文件内容是 JSON，进程若在此刻退出，下次 <c>Load</c> 会拿 <c>CSVEx.ReadCsv</c> 去读 JSON；</item>
/// <item>CSV 覆盖失败（CSV 被 Excel 占住是现场最常见的一种）时文件就永久停在 JSON 上；</item>
/// <item>异常从 async-void lambda 逃逸出来直接崩进程。</item>
/// </list>
///
/// <para>
/// 另一条独立缺陷：<see cref="AlarmInfoManagerService.Load"/> 在文件缺失时抛
/// <c>new Exception("File Not Exist!!")</c>，而它由<b>构造函数</b>调用 —— 构造函数又被
/// <c>AutoProcess</c>（生产流程启动）与 <c>LogAlarmInfoService</c>（每条报警日志）触发，
/// 于是一份缺失的 CSV 能让整条生产流程起不来，且报错只有一句 "File Not Exist!!"。
/// </para>
///
/// <para>
/// 本套件钉住：① 缺文件 / 读失败**不抛**且 <c>DataCollection</c> 恒非 null（有说明文字）；
/// ② <c>Save</c> 的顺序是 校验 → 落盘，任一步失败都不动磁盘、也不丢用户正在编辑的内容；
/// ③ 页面命令失败时只报错、不谎报成功，且不再经 async-void 回调。
/// </para>
///
/// <para>
/// ⚠️ <c>AlarmInfoManagerService</c> 的构造函数从容器解析 <c>LogInfoService</c> /
/// <c>ISecsGemRuntime</c> / <c>EPPlusHelper</c>，所以本套件必须搭一个最小 DryIoc 容器
/// （<c>LogInfoServiceRecursionTests</c> 是同一套做法的先例）。三个协作对象在持久化路径上
/// **一个都不会被真的用到**，只为让构造函数能跑完。
/// </para>
/// </summary>
public sealed class AlarmConfigPersistenceTests
{
    private const string ConfigFileName = "Alarm.csv";

    // ============================================================ 加载：缺文件不抛

    /// <summary>
    /// <b>缺文件 ⇒ 不抛、空表、并说明原因。</b>这是 T12b 的第一条核心缺陷。
    ///
    /// <para>
    /// 改造前 <c>Load()</c> 抛 <c>new Exception("File Not Exist!!")</c>，而它是构造函数的最后一步
    /// ⇒ <c>container.Resolve&lt;AlarmInfoManagerService&gt;()</c> 抛 ⇒ <c>AutoProcess</c> 构造失败。
    /// </para>
    /// </summary>
    [Fact]
    public Task Load_MissingFile_LeavesEmptyNonNullCollectionAndExplainsWhy() => OnDispatcher(() =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();

        // 隔离目录里没有 Alarm.csv（生产默认是相对的 "Config//"，测试输出目录里同样没有）。
        UseDirectory(service, scope.Directory);

        service.Load();     // 改造前：这里抛裸 Exception("File Not Exist!!")

        Assert.NotNull(service.Model.DataCollection);
        Assert.Empty(service.Model.DataCollection);
        Assert.Contains("未找到报警配置文件", service.LoadMessage, StringComparison.Ordinal);
        return Task.CompletedTask;
    });

    /// <summary>
    /// <b>构造函数本身不能因为文件缺失而失败</b>：默认 <c>Model.FilePath = "Config//"</c> 是相对
    /// 当前工作目录的，测试输出目录下没有 <c>Config\Alarm.csv</c>。
    /// 本用例能构造出服务实例即为通过（改造前会在此抛异常）。
    /// </summary>
    [Fact]
    public Task Constructor_WithDefaultRelativeConfigPath_DoesNotThrow() => OnDispatcher(() =>
    {
        var provider = CreateProvider();

        var service = provider.Resolve<AlarmInfoManagerService>();

        Assert.NotNull(service.Model.DataCollection);
        return Task.CompletedTask;
    });

    /// <summary>
    /// 文件存在时按 CSV 正常读入，且不留下加载期说明。
    /// </summary>
    [Fact]
    public Task Load_ExistingFile_ReadsRowsFromDiskAndClearsMessage() => OnDispatcher(() =>
    {
        using var scope = new ConfigScope();
        WriteCsv(scope.Directory, "ID,Address,Info_CH,Info_EN,Enable", "1,D6000.0,甲,AA", "2,D6000.1,乙,BB");

        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        service.Load();

        Assert.Equal(new[] { 1, 2 }, service.Model.DataCollection.Select(row => row.ID));
        Assert.Equal("D6000.0", service.Model.DataCollection[0].Address);
        Assert.Equal("甲", service.Model.DataCollection[0].Info_CH);
        // Enable 列是空字段 ⇒ CSVEx.ReadCsv 跳过 ⇒ 保持默认 true。
        Assert.True(service.Model.DataCollection[0].Enable);
        Assert.Equal(string.Empty, service.LoadMessage);
        return Task.CompletedTask;
    });

    /// <summary>
    /// 内容损坏（表头对得上、但 ID 列不是整数）⇒ 同样不抛，退化成空表 + 说明。
    /// 改造前这里会被 <c>CSVEx.ReadCsv</c> 的裸 <c>Exception</c> 穿透构造函数。
    ///
    /// <para>
    /// ⚠️ 必须用「表头能匹配、单元格转换失败」的形态：<c>ReadCsv</c> 对<b>只有表头</b>或
    /// 表头匹配不上的文件是**静默返回空列表**（不抛），拿那种内容测不出这条路径。
    /// </para>
    /// </summary>
    [Fact]
    public Task Load_CorruptFile_LeavesEmptyCollectionAndExplainsWhy() => OnDispatcher(() =>
    {
        using var scope = new ConfigScope();
        WriteCsv(scope.Directory, "ID,Address,Info_CH,Info_EN,Enable", "not-a-number,D6000.0,甲,AA");

        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        service.Load();

        Assert.NotNull(service.Model.DataCollection);
        Assert.Empty(service.Model.DataCollection);
        Assert.Contains("读取失败", service.LoadMessage, StringComparison.Ordinal);
        return Task.CompletedTask;
    });

    // ============================================================ 保存：成功路径

    /// <summary>
    /// 保存成功：磁盘上是刚编辑的内容，且再 <c>Load</c> 一次能原样读回来（等价于下次启动）。
    /// </summary>
    [Fact]
    public Task Save_WritesEditedRowsToDiskAndRoundTripsThroughLoad() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        service.Model.DataCollection = new(
        [
            Row(1, "D6000.0", "甲"),
            Row(2, "D6000.1", "乙"),
        ]);

        await service.Save();

        var reloaded = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(reloaded, scope.Directory);
        reloaded.Load();

        Assert.Equal(new[] { 1, 2 }, reloaded.Model.DataCollection.Select(row => row.ID));
        Assert.Equal(new[] { "D6000.0", "D6000.1" },
            reloaded.Model.DataCollection.Select(row => row.Address));
        Assert.Equal(new[] { "甲", "乙" }, reloaded.Model.DataCollection.Select(row => row.Info_CH));
        Assert.Equal(string.Empty, reloaded.LoadMessage);
    });

    // ============================================================ 保存：失败路径

    /// <summary>
    /// <b>报警ID 重复 ⇒ 既不写盘也不改内存。</b>
    /// <c>TriggerAlarm</c> 用 <c>AlarmInfos.TryAdd(id, ...)</c> 登记，重复 ID 会静默丢弃第二条
    /// —— 在半导体设备上这是安全问题，必须在写盘前拦住。
    /// </summary>
    [Fact]
    public Task Save_WithDuplicateId_WritesNothingAndKeepsEdits() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        // 磁盘上先有一份合法配置。
        service.Model.DataCollection = new([Row(1, "D6000.0", "甲")]);
        await service.Save();
        var configFile = Path.Combine(scope.Directory, ConfigFileName);
        var diskBefore = File.ReadAllText(configFile);

        // 界面上改成两条重复 ID。
        service.Model.DataCollection = new([Row(1, "D6000.0", "甲"), Row(1, "D6000.1", "乙")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Save());

        Assert.Equal(diskBefore, File.ReadAllText(configFile));                        // 磁盘没动
        Assert.Equal(2, service.Model.DataCollection.Count);                           // 编辑内容保留
    });

    /// <summary>
    /// <b>寄存器地址为空 ⇒ 既不写盘也不改内存。</b>
    /// 空单元格被 <c>CSVEx.ReadCsv</c> 跳过 ⇒ <c>Address</c> 读回来是 <c>null</c> ⇒
    /// <c>Organize</c> 的 <c>x.Address.Contains(...)</c> 抛 <c>NullReferenceException</c>，报警扫描线程直接死。
    /// </summary>
    [Fact]
    public Task Save_WithBlankAddress_WritesNothingAndKeepsEdits() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        service.Model.DataCollection = new([Row(1, "D6000.0", "甲")]);
        await service.Save();
        var configFile = Path.Combine(scope.Directory, ConfigFileName);
        var diskBefore = File.ReadAllText(configFile);

        service.Model.DataCollection = new([Row(1, "   ", "甲")]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.Save());

        Assert.Equal(diskBefore, File.ReadAllText(configFile));
        Assert.Single(service.Model.DataCollection);
    });

    /// <summary>
    /// 路径 / 文件名未设置 ⇒ 抛 <see cref="InvalidOperationException"/>（改造前会拿
    /// <c>Path.Combine(null, ...)</c> 抛 <see cref="ArgumentNullException"/>，报错看不懂）。
    /// </summary>
    [Fact]
    public Task Save_WithoutPathOrFileName_FailsWithoutWriting() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();

        service.Model.FilePath = null;
        service.Model.FileName = null;
        service.Model.DataCollection = new([Row(1, "D6000.0", "甲")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Save());

        Assert.False(File.Exists(Path.Combine(scope.Directory, ConfigFileName)));
    });

    /// <summary>
    /// <b>写盘失败（CSV 被别的进程占住）⇒ 磁盘原样、编辑内容原样、异常照抛。</b>
    ///
    /// <para>
    /// 这是现场最真实的失败：操作员用 Excel 打开了 <c>Alarm.csv</c>。
    /// <c>CSVEx.ExportToCsvAsync</c> 是 <c>FileMode.Create</c> + <c>FileShare.None</c>，
    /// 打开失败发生在<b>截断之前</b> ⇒ 旧文件不会被清空。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 已知残留（本任务不修，记在报告里）：若 <c>FileStream</c> 已成功打开、写入过程中途失败
    /// （磁盘满 / 设备错误），<c>FileMode.Create</c> 已经把原文件截断，磁盘会停在半截内容上。
    /// 要彻底闭合需要「写临时文件 + 原子替换」，属于 <c>CSVEx</c> 的共享改造，不在本页范围内。
    /// </para>
    /// </summary>
    [Fact]
    public Task Save_WhenFileIsLocked_FailsWithoutTouchingDiskOrEdits() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        service.Model.DataCollection = new([Row(1, "D6000.0", "甲")]);
        await service.Save();
        var configFile = Path.Combine(scope.Directory, ConfigFileName);
        var diskBefore = File.ReadAllText(configFile);

        service.Model.DataCollection = new([Row(2, "D6000.1", "乙")]);

        // 独占打开（模拟 Excel）：ExportToCsvAsync 的 FileStream 打不开 ⇒ 在截断前就失败。
        using (var hold = new FileStream(configFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => service.Save());
        }

        Assert.Equal(diskBefore, File.ReadAllText(configFile));      // 磁盘原样
        Assert.Equal(2, service.Model.DataCollection[0].ID);         // 编辑内容原样
    });

    // ============================================================ 页面命令（失败不谎报成功）

    /// <summary>
    /// 进入页面时把加载期问题摆到状态行上：报警表空着但没人知道为什么是最糟的状态。
    /// </summary>
    [Fact]
    public Task ViewModel_SurfacesLoadMessageAtStartup() => OnDispatcher(() =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);
        service.Load();

        var viewModel = new AlarmInfoManagerViewModel(provider);

        Assert.Contains("未找到报警配置文件", viewModel.StatusText, StringComparison.Ordinal);
        return Task.CompletedTask;
    });

    /// <summary>
    /// 保存成功时状态行给出落盘位置，且不抛异常。
    /// </summary>
    [Fact]
    public Task ViewModel_SuccessfulSave_ReportsPathInStatusText() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);
        service.Model.DataCollection = new([Row(1, "D6000.0", "甲")]);
        var viewModel = new AlarmInfoManagerViewModel(provider);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.StartsWith("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains(ConfigFileName, viewModel.StatusText, StringComparison.Ordinal);
    });

    /// <summary>
    /// <b>保存失败时：命令不抛（抛出去会冒到 Dispatcher 把界面带崩）、状态行写明失败、
    /// 不出现"已保存"字样、用户正在编辑的内容保留。</b>
    /// 改造前这条路径是 async-void lambda ⇒ 异常逃逸 ⇒ 进程崩溃。
    /// </summary>
    [Fact]
    public Task ViewModel_FailedSave_ReportsFailureWithoutClaimingSuccessAndKeepsEdits() => OnDispatcher(async () =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);
        service.Model.DataCollection = new([Row(1, "D6000.0", "甲")]);
        var viewModel = new AlarmInfoManagerViewModel(provider);

        // 让它必然失败：两条重复 ID。
        service.Model.DataCollection = new([Row(7, "D6000.0", "甲"), Row(7, "D6000.1", "乙")]);

        Assert.Null(await Record.ExceptionAsync(() => viewModel.SaveCommand.ExecuteAsync(null)));

        Assert.StartsWith("保存失败，配置未改变", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(2, viewModel.Datas.DataCollection.Count);
    });

    /// <summary>
    /// 页面暴露的是可绑到 <c>ConfigManagerView.SaveCommand</c> 的 <see cref="ICommand"/>，
    /// 不再是「像命令其实是 Action」的 <c>SaveCallbackCommand</c>（其内容是 async-void lambda）。
    /// </summary>
    [Fact]
    public Task ViewModel_ExposesSaveCommandAsICommandAndDropsCallbackSeam() => OnDispatcher(() =>
    {
        using var scope = new ConfigScope();
        var provider = CreateProvider();
        var service = provider.Resolve<AlarmInfoManagerService>();
        UseDirectory(service, scope.Directory);

        var viewModel = new AlarmInfoManagerViewModel(provider);

        Assert.IsAssignableFrom<ICommand>(viewModel.SaveCommand);
        Assert.Null(typeof(AlarmInfoManagerViewModel).GetProperty("SaveCallbackCommand"));
        return Task.CompletedTask;
    });

    // ============================================================ 工具

    /// <summary>把服务的落盘位置指到隔离目录（生产默认是相对的 <c>Config//</c>）。</summary>
    private static void UseDirectory(AlarmInfoManagerService service, string directory)
    {
        service.Model.FilePath = directory;
        service.Model.FileName = ConfigFileName;
    }

    /// <summary>构造一条报警配置。</summary>
    private static AlarmInfoManager Row(int id, string address, string infoCh) => new()
    {
        ID = id,
        Address = address,
        Info_CH = infoCh,
        Info_EN = "E" + id,
        Enable = true,
    };

    /// <summary>按 <c>CSVEx</c> 的表头口径写一份最小 CSV。</summary>
    private static void WriteCsv(string directory, params string[] lines)
    {
        System.IO.Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, ConfigFileName), lines);
    }

    /// <summary>
    /// 搭一个最小 DryIoc 容器，只为让 <see cref="AlarmInfoManagerService"/> 的构造函数能跑完。
    /// 三个协作对象在持久化路径上都不会被真的调用（<see cref="NullSecsGemRuntime"/> 一被调用就抛，
    /// 正好把「测试意图出错」暴露出来）。
    /// </summary>
    private static IContainerProvider CreateProvider()
    {
        var container = new Container();
        container.RegisterInstance<ILogManager>(new LogManager());
        container.RegisterInstance<ISecsGemRuntime>(new NullSecsGemRuntime());
        container.RegisterInstance(new TryCatchHelper());
        container.Register<LogAlarmInfoService>(Reuse.Singleton);
        container.Register<LogInfoService>(Reuse.Singleton);
        container.Register<EPPlusHelper>(Reuse.Singleton);
        container.Register<AlarmInfoManagerService>(Reuse.Singleton);

        // ⚠️ 别在这里再 RegisterInstance<IContainerProvider>：DryIocContainerExtension 的构造函数
        // **自己**就把 this 注册成了 IContainerExtension / IContainerProvider / DryIocContainerExtension。
        // 手动再注册一次会让同一服务有两个默认注册，解析时抛
        // `Error.ExpectedSingleDefaultFactory`（LogInfoServiceRecursionTests 第一版踩过）。
        return new DryIocContainerExtension(container);
    }

    /// <summary>临时目录作用域：退出时整棵删除。</summary>
    private sealed class ConfigScope : IDisposable
    {
        public ConfigScope()
        {
            Directory = Path.Combine(Path.GetTempPath(), "SF-AlarmConfig-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string Directory { get; }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                    System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// <see cref="ISecsGemRuntime"/> 的空实现。报警配置的读写完全不用 SECS/GEM，
    /// 这里只为让构造函数能跑完；任何成员被真的调到都说明测试意图出错，故直接抛。
    /// </summary>
    private sealed class NullSecsGemRuntime : ISecsGemRuntime
    {
        public SecsGemState GetState() => throw new NotSupportedException();

        public Task<bool> ConnectAsync(SecsGemOptions options) => throw new NotSupportedException();

        public Task DisconnectAsync() => throw new NotSupportedException();

        public Task EstablishCommunicationAsync() => throw new NotSupportedException();

        public Task<bool> GoOnlineLocalAsync() => throw new NotSupportedException();

        public Task<bool> GoOnlineRemoteAsync() => throw new NotSupportedException();

        public Task<bool> GoOfflineAsync() => throw new NotSupportedException();

        public Task ShutdownAsync(TimeSpan notifyTimeout) => throw new NotSupportedException();

        public void SetVariable(string name, object value) => throw new NotSupportedException();

        public void AppendVariable(string name, object value) => throw new NotSupportedException();

        public string? GetVariableValue(string name) => throw new NotSupportedException();

        public Task<bool> ReportEventAsync(string eventName) => throw new NotSupportedException();

        public Task<bool> ReportAlarmAsync(SecsGemAlarm alarm) => throw new NotSupportedException();

        public void SetEventHandler(ISecsGemEventHandler? handler) => throw new NotSupportedException();

        public void SetEquipmentDataProvider(IEquipmentDataProvider? provider) => throw new NotSupportedException();

        public void Dispose() { }
    }

    /// <summary>
    /// 在带 Dispatcher 的 STA 线程上跑测试体（照搬 <c>AlarmHistoryHostTests.OnDispatcher</c>）：
    /// <c>LogInfoService</c> 继承的 <c>DataGridService&lt;T&gt;</c> 在构造期就建 <c>DispatcherTimer</c>，
    /// 而页面命令的失败路径也必须在真实的 UI 上下文里验证（"异常会不会冒到 Dispatcher"）。
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
