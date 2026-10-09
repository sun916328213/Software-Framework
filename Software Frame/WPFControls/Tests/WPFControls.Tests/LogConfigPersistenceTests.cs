// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogConfigPersistenceTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log、DataManager.Core.Log.Definition、Extension.Core.Conversion、WPFControls.ProgramControl.Services、WPFControls.ProgramControl.ViewModels、System.IO。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using Extension.Core.Conversion;
using WPFControls.ProgramControl.Services;
using WPFControls.ProgramControl.ViewModels;
using Xunit;

namespace WPFControls.Tests;

/// <summary>
/// T12a：日志配置的「落盘 + 生效」归属。
///
/// <para>
/// 背景：改造前保存按钮由 <c>ConfigManagerView.SaveData</c> 自己 <c>JsonEx.Save</c>，
/// 然后在 try/catch <b>之外</b>调 <c>SaveCallback</c> → <c>LogConfigService.Load()</c>。
/// 于是写盘失败时照样重载 —— 界面刚弹完"保存数据时出错"，运行中的日志实例
/// 已经被磁盘上的<b>旧文件</b>覆盖回去，用户以为改坏了、实际改丢了。
/// </para>
///
/// <para>
/// 本套件钉住 <see cref="LogConfigService.Save"/> 的顺序（校验 → 落盘 → 生效）与
/// 「任一步失败都不执行后续步骤」，以及页面命令在失败时只报错、不谎报成功。
/// 用记录型 <see cref="ILogManager"/> 桩：断言的是「有没有被要求替换、替换成什么」，
/// 与真实 LogManager 的实例构造解耦（后者的替换语义已由
/// <c>WPFControls.Host.Tests/HostFlowLogIntegrationTests</c> 覆盖）。
/// </para>
/// </summary>
public sealed class LogConfigPersistenceTests
{
    // ============================================================ 成功路径

    /// <summary>
    /// 保存成功：磁盘上是刚编辑的内容，运行态也换成了同一份内容。
    /// </summary>
    [Fact]
    public void Save_WritesEditedConfigToDiskAndAppliesIt()
    {
        using var scope = new ConfigScope();
        var manager = new RecordingLogManager();
        var service = new LogConfigService(manager);
        var baseline = manager.Applied.Count;
        UseDirectory(service, scope.Directory);

        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("RunLog", Path.Combine(scope.Directory, "RunLog")),
            Param("FlowLog", Path.Combine(scope.Directory, "FlowLog")),
        };

        service.Save();

        // ① 落盘
        var onDisk = JsonEx.Read<ObservableCollection<LogParam>>(scope.Directory, ConfigFileName);
        Assert.NotNull(onDisk);
        Assert.Equal(new[] { "RunLog", "FlowLog" }, onDisk!.Select(parameter => parameter.Name));
        Assert.Equal(Path.Combine(scope.Directory, "RunLog"),
            onDisk.Single(parameter => parameter.Name == "RunLog").FilePath);

        // ② 生效：Apply 拿到的就是刚编辑的内容。
        Assert.Equal(baseline + 1, manager.Applied.Count);
        Assert.Equal(new[] { "RunLog", "FlowLog" },
            manager.Applied[^1].Select(parameter => parameter.Name));
        // ReplaceConfigured 的第一个参数是「调用方**原先**拥有的名字」——本次是空集
        // （此前没有任何已生效实例），这正是整体替换的语义：先交还旧的，再装上新的。
        Assert.Empty(manager.OwnedNames[^1]);

        // ③ 服务已把「自己拥有的名字」更新成新集合：再存一次时，交还的旧集合就是它们。
        service.Save();
        Assert.Equal(new[] { "RunLog", "FlowLog" }, manager.OwnedNames[^1]);
        Assert.Equal(baseline + 2, manager.Applied.Count);
    }

    /// <summary>
    /// 保存后重新加载（等价于下次启动）：磁盘内容能原样读回来并再次生效。
    /// </summary>
    [Fact]
    public void SaveThenLoad_RoundTripsThroughDisk()
    {
        using var scope = new ConfigScope();
        var manager = new RecordingLogManager();
        var service = new LogConfigService(manager);
        UseDirectory(service, scope.Directory);

        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("SEMILog", Path.Combine(scope.Directory, "SEMILog")),
        };
        service.Save();
        var appliedAfterSave = manager.Applied.Count;

        // 第二个服务实例只经构造函数 Load()（与生产启动路径一致）。
        var reloaded = new LogConfigService(manager);
        UseDirectory(reloaded, scope.Directory);
        reloaded.Load();

        Assert.Equal(new[] { "SEMILog" }, reloaded.Model.DataCollection.Select(parameter => parameter.Name));
        Assert.Equal(new[] { "SEMILog" }, manager.Applied[^1].Select(parameter => parameter.Name));
        Assert.Equal(appliedAfterSave + 2, manager.Applied.Count);
    }

    // ============================================================ 失败路径（本任务的核心）

    /// <summary>
    /// <b>写盘失败 ⇒ 不生效</b>：这是 T12a 要修的那条缺陷。
    ///
    /// <para>
    /// 让写盘必然失败（FilePath 指到一个已存在的<b>文件</b>上，补建目录即抛 IOException），
    /// 然后断言三件事：① 没有 Apply（运行态一个字节没动）；
    /// ② 没有重载旧数据（<c>Model</c> 里仍是用户正在编辑的内容，不是磁盘上的旧内容）；
    /// ③ 磁盘没被动过。
    /// </para>
    /// </summary>
    [Fact]
    public void Save_WhenWriteFails_DoesNotApplyAndKeepsEditsAndDiskIntact()
    {
        using var scope = new ConfigScope();
        var manager = new RecordingLogManager();
        var service = new LogConfigService(manager);
        UseDirectory(service, scope.Directory);

        // 磁盘上先有一份「旧配置」，并已生效。
        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("RunLog", Path.Combine(scope.Directory, "RunLog-old")),
        };
        service.Save();
        var appliedBefore = manager.Applied.Count;
        var configFile = Path.Combine(scope.Directory, ConfigFileName);
        var diskBefore = File.ReadAllText(configFile);

        // 让写盘必然失败：Path.Combine(blocker, ...) 的父级是一个**文件**，补建目录会抛 IOException。
        var blocker = Path.Combine(scope.Directory, "not-a-directory");
        File.WriteAllText(blocker, "x");
        service.Model.FilePath = blocker;

        // 界面上改成新内容。
        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("FlowLog", Path.Combine(scope.Directory, "FlowLog-new")),
        };

        Assert.Throws<IOException>(() => service.Save());

        // ① 没有 Apply。
        Assert.Equal(appliedBefore, manager.Applied.Count);
        // ② 没有重载旧数据：编辑内容原样保留（改造前这里会被磁盘旧内容顶掉）。
        Assert.Equal(new[] { "FlowLog" },
            service.Model.DataCollection.Select(parameter => parameter.Name));
        // ③ 磁盘没被动过。
        Assert.Equal(diskBefore, File.ReadAllText(configFile));
    }

    /// <summary>
    /// <b>校验失败 ⇒ 既不写盘也不生效</b>：坏配置不该被写进文件，更不该顶掉正在跑的日志。
    /// 覆盖两类非法项：缺必填字段（<see cref="ArgumentException"/>）与重名（<see cref="InvalidOperationException"/>）。
    /// </summary>
    [Fact]
    public void Save_WithInvalidConfig_WritesNothingAndKeepsRunningInstances()
    {
        using var scope = new ConfigScope();
        var manager = new RecordingLogManager();
        var service = new LogConfigService(manager);
        UseDirectory(service, scope.Directory);

        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("RunLog", Path.Combine(scope.Directory, "RunLog")),
        };
        service.Save();
        var appliedBefore = manager.Applied.Count;
        var configFile = Path.Combine(scope.Directory, ConfigFileName);
        var diskBefore = File.ReadAllText(configFile);

        // ① 缺必填字段
        service.Model.DataCollection = new ObservableCollection<LogParam> { Param(string.Empty, "dir") };
        Assert.Throws<ArgumentException>(() => service.Save());
        Assert.Equal(appliedBefore, manager.Applied.Count);
        Assert.Equal(diskBefore, File.ReadAllText(configFile));

        // ② 重名
        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("RunLog", Path.Combine(scope.Directory, "a")),
            Param("RunLog", Path.Combine(scope.Directory, "b")),
        };
        Assert.Throws<InvalidOperationException>(() => service.Save());
        Assert.Equal(appliedBefore, manager.Applied.Count);
        Assert.Equal(diskBefore, File.ReadAllText(configFile));
    }

    /// <summary>
    /// 路径 / 文件名未设置时保存必须失败并保持运行态不变（改造前这里会静默返回，界面无从知道没保存）。
    /// </summary>
    [Fact]
    public void Save_WithoutFilePathOrFileName_FailsWithoutApplying()
    {
        using var scope = new ConfigScope();
        var manager = new RecordingLogManager();
        var service = new LogConfigService(manager);
        var appliedBefore = manager.Applied.Count;

        service.Model.FilePath = null;
        service.Model.FileName = null;
        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("RunLog", Path.Combine(scope.Directory, "RunLog")),
        };

        Assert.Throws<InvalidOperationException>(() => service.Save());
        Assert.Equal(appliedBefore, manager.Applied.Count);
    }

    // ============================================================ 页面命令（失败不谎报成功）

    /// <summary>
    /// 保存成功时状态行给出落盘位置，且不抛异常。
    /// </summary>
    [Fact]
    public void ViewModel_SuccessfulSave_ReportsPathInStatusText()
    {
        using var scope = new ConfigScope();
        var service = new LogConfigService(new RecordingLogManager());
        UseDirectory(service, scope.Directory);
        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("RunLog", Path.Combine(scope.Directory, "RunLog")),
        };
        var viewModel = new LogConfigViewModel(service);

        Assert.Equal(string.Empty, viewModel.StatusText);
        viewModel.SaveCommand.Execute(null);

        Assert.StartsWith("已保存并生效", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains(ConfigFileName, viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 保存失败时：命令不抛（抛出去会冒到 Dispatcher 把界面带崩）、状态行写明失败、
    /// <b>不</b>出现成功字样、用户正在编辑的内容保留。
    /// </summary>
    [Fact]
    public void ViewModel_FailedSave_ReportsFailureWithoutClaimingSuccessAndKeepsEdits()
    {
        using var scope = new ConfigScope();
        var manager = new RecordingLogManager();
        var service = new LogConfigService(manager);
        UseDirectory(service, scope.Directory);

        var blocker = Path.Combine(scope.Directory, "not-a-directory");
        File.WriteAllText(blocker, "x");
        service.Model.FilePath = blocker;
        service.Model.DataCollection = new ObservableCollection<LogParam>
        {
            Param("FlowLog", Path.Combine(scope.Directory, "FlowLog-new")),
        };
        var appliedBefore = manager.Applied.Count;
        var viewModel = new LogConfigViewModel(service);

        Assert.Null(Record.Exception(() => viewModel.SaveCommand.Execute(null)));

        Assert.StartsWith("保存失败，配置未生效", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("已保存", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(appliedBefore, manager.Applied.Count);
        Assert.Equal(new[] { "FlowLog" },
            viewModel.Datas.DataCollection.Select(parameter => parameter.Name));
    }

    /// <summary>
    /// 页面暴露的是可绑到 <c>ConfigManagerView.SaveCommand</c> 的 <see cref="ICommand"/>，
    /// 不再是「像命令其实是 Action」的 <c>SaveCallbackCommand</c>。
    /// </summary>
    [Fact]
    public void ViewModel_ExposesSaveCommandAsICommandAndDropsCallbackSeam()
    {
        using var scope = new ConfigScope();
        var service = new LogConfigService(new RecordingLogManager());
        UseDirectory(service, scope.Directory);

        var viewModel = new LogConfigViewModel(service);

        Assert.IsAssignableFrom<ICommand>(viewModel.SaveCommand);
        Assert.Null(typeof(LogConfigViewModel).GetProperty("SaveCallbackCommand"));
    }

    // ============================================================ 工具

    private const string ConfigFileName = "LogConfig.json";

    /// <summary>把服务的落盘位置指到隔离目录（生产默认是 <c>Config//</c>）。</summary>
    private static void UseDirectory(LogConfigService service, string directory)
    {
        service.Model.FilePath = directory;
        service.Model.FileName = ConfigFileName;
    }

    /// <summary>构造一项合法日志配置。</summary>
    private static LogParam Param(string name, string filePath) => new()
    {
        Name = name,
        FilePath = filePath,
        Separator = "||",
        SaveDays = 30,
        LogType = ELogType.SeriLog,
    };

    /// <summary>临时目录作用域：退出时整棵删除。</summary>
    private sealed class ConfigScope : IDisposable
    {
        public ConfigScope()
        {
            Directory = Path.Combine(Path.GetTempPath(), "SF-LogConfig-" + Guid.NewGuid().ToString("N"));
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
    /// 记录每次 <see cref="ILogManager.ReplaceConfigured"/> 的桩。
    /// 只关心「有没有被要求替换、替换成什么」——实例构造语义由真实 LogManager 的集成测试覆盖。
    /// </summary>
    private sealed class RecordingLogManager : ILogManager
    {
        public List<IReadOnlyList<LogParam>> Applied { get; } = [];

        public List<IReadOnlyCollection<string>> OwnedNames { get; } = [];

        public IReadOnlyList<ILog> Items => [];

        public int Count => 0;

        public void ReplaceConfigured(
            IReadOnlyCollection<string> ownedNames,
            IReadOnlyList<LogParam> parameters)
        {
            OwnedNames.Add(ownedNames.ToArray());
            Applied.Add(parameters.ToArray());
        }

        public bool TryGet(string name, out ILog? item)
        {
            item = null;
            return false;
        }

        public void Config(List<LogParam> param) => throw new NotSupportedException();

        public ILog Create(LogParam parameter) => throw new NotSupportedException();

        public void Add(ILog item) => throw new NotSupportedException();

        public bool Remove(ILog item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();
    }
}
