// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HostFlowLogIntegrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、FlowEditor.Core、FlowEditor.Runtime、System.Diagnostics。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using FlowEditor.Core;
using FlowEditor.Runtime;
using System.Diagnostics;
using System.Text.Json;
using FlowEditor.DataManagerLogging;
using Xunit;

namespace WPFControls.Host.Tests;

/// <summary>
/// HostFlowLogIntegrationTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class HostFlowLogIntegrationTests
{
    /// <summary>
    /// 验证逻辑路由名称不会借助分隔符增加目录层级，且路径清理不会合并不同流程。
    /// </summary>
    [Fact]
    public void Write_UsesCollisionSafeBoundedDirectorySegments()
    {
        var root = Path.Combine(Path.GetTempPath(), $"flow-log-route-{Guid.NewGuid():N}");
        var logDirectory = Path.Combine(root, "logs");

        var manager = new LogManager();
        RegisterBaseFlowLog(manager, logDirectory);

        try
        {
            var sink = new DataManagerFlowLogSink(manager);

            sink.Write(CreateRecord("colon", FlowLogLevel.Information, "ProcessA", "A:B"));
            sink.Write(CreateRecord("question", FlowLogLevel.Information, "ProcessA", "A?B"));
            sink.Write(CreateRecord("separator", FlowLogLevel.Information, "Area\\One", "Flow\\Child"));
            sink.Write(CreateRecord("bounded", FlowLogLevel.Information, "CON", new string('L', 180)));
            sink.Write(CreateRecord("base collision", FlowLogLevel.Information, string.Empty, "FlowLog"));

            var dynamicLogs = manager.Items.Where(log => log.Param.Name != "FlowLog").ToList();
            Assert.Equal(5, dynamicLogs.Count);
            Assert.Equal(5, dynamicLogs.Select(log => log.Param.FilePath).Distinct().Count());

            var normalizedCollisionCandidates = dynamicLogs
                .Where(log => log.Param.Name is "ProcessA\\A:B" or "ProcessA\\A?B")
                .Select(log => Path.GetFileName(log.Param.FilePath))
                .ToList();
            Assert.Equal(2, normalizedCollisionCandidates.Distinct().Count());

            var separatorLog = Assert.Single(dynamicLogs, log =>
                log.Param.Name.StartsWith("Area\\One", StringComparison.Ordinal));
            Assert.Equal(2, GetRelativeSegments(logDirectory, separatorLog.Param.FilePath).Length);

            var baseNameCollisionLog = Assert.Single(dynamicLogs, log =>
                log.Param.Name.StartsWith("FlowLog|route:", StringComparison.Ordinal));
            Assert.Single(GetRelativeSegments(logDirectory, baseNameCollisionLog.Param.FilePath));

            foreach (var log in dynamicLogs)
            {
                var segments = GetRelativeSegments(logDirectory, log.Param.FilePath);
                Assert.All(segments, segment => Assert.InRange(segment.Length, 1, 96));
                Assert.DoesNotContain(segments, segment => segment.Equals("CON", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            foreach (var log in manager.Items.OfType<IDisposable>())
                log.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>验证宿主日志故障会产生备用诊断，同时仍不向流程调用方抛出。</summary>
    [Fact]
    public void Write_BackendFailure_IsReportedThroughTraceWithoutEscaping()
    {
        var root = Path.Combine(Path.GetTempPath(), $"flow-log-diagnostic-{Guid.NewGuid():N}");
        var manager = new LogManager();
        RegisterBaseFlowLog(manager, Path.Combine(root, "logs"));
        var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);

        try
        {
            var sink = new DataManagerFlowLogSink(manager);
            Trace.Listeners.Add(listener);

            var exception = Record.Exception(() => sink.Write(
                CreateRecord("invalid level", (FlowLogLevel)int.MaxValue)));
            listener.Flush();

            Assert.Null(exception);
            Assert.Contains("[FlowLog] 宿主日志写入失败", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            foreach (var log in manager.Items.OfType<IDisposable>())
                log.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 验证宿主日志配置可重载，动态实例会重建，且日志等级不丢失。
    /// </summary>
    [Fact]
    public void Reload_ReplacesBaseAndDynamicLogs_AndPreservesLevels()
    {
        var root = Path.Combine(Path.GetTempPath(), $"flow-log-host-{Guid.NewGuid():N}");
        var firstLogDirectory = Path.Combine(root, "first");
        var secondLogDirectory = Path.Combine(root, "second");

        var manager = new LogManager();
        RegisterBaseFlowLog(manager, firstLogDirectory);

        try
        {
            var sink = new DataManagerFlowLogSink(manager);

            foreach (var level in Enum.GetValues<FlowLogLevel>())
                sink.Write(CreateRecord($"first {level}", level));

            var firstBaseLog = manager["FlowLog"];
            var firstDynamicLog = manager["ProcessA\\FlowA"];
            Assert.NotNull(firstBaseLog);
            Assert.NotNull(firstDynamicLog);
            Assert.Equal(2, manager.Count);

            // 宿主重载配置（等价于 LogConfigService.Load）：基础实例被整体替换。
            // 动态实例改为惰性失效 —— 下次写入时才重建，adapter 不必反向订阅宿主事件。
            manager.ReplaceConfigured(["FlowLog"], [CreateTemplate(secondLogDirectory)]);
            Assert.NotSame(firstBaseLog, manager["FlowLog"]);

            sink.Write(CreateRecord("second error", FlowLogLevel.Error));
            var secondDynamicLog = manager["ProcessA\\FlowA"];
            Assert.NotNull(secondDynamicLog);
            Assert.NotSame(firstDynamicLog, secondDynamicLog);
            Assert.StartsWith(secondLogDirectory, secondDynamicLog.Param.FilePath, StringComparison.Ordinal);
            Assert.Equal(2, manager.Count);

            (secondDynamicLog as IDisposable)?.Dispose();
            foreach (var level in Enum.GetValues<FlowLogLevel>())
                AssertLogContains(firstLogDirectory, level.ToString(), $"first {level}");
            AssertLogContains(secondLogDirectory, "Error", "second error");
        }
        finally
        {
            foreach (var log in manager.Items.OfType<IDisposable>())
                log.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>验证 Packing Machine 实际 Log 配置可驱动流程路由和等级落盘。</summary>
    [Fact]
    public void MachineAppConfig_FlowLogTemplate_DrivesRoutedLevelledOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), $"flow-log-packing-{Guid.NewGuid():N}");
        var logDirectory = Path.Combine(root, "logs");
        var manager = new LogManager();
        var configPath = FindMachineAppLogConfig();
        var configured = JsonSerializer.Deserialize<List<LogParam>>(
                             File.ReadAllText(configPath),
                             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? throw new InvalidDataException("Packing Machine 日志配置为空。");
        var actualTemplate = Assert.Single(configured, parameter => parameter.Name == "FlowLog");
        Assert.Equal(@"D:\DeviceData\Log\FlowLog", actualTemplate.FilePath);
        Assert.Equal("||", actualTemplate.Separator);
        Assert.Equal(180, actualTemplate.SaveDays);
        Assert.Equal(ELogType.SeriLog, actualTemplate.LogType);

        var redirectedTemplate = new LogParam
        {
            Name = actualTemplate.Name,
            FilePath = logDirectory,
            Separator = actualTemplate.Separator,
            SaveDays = actualTemplate.SaveDays,
            LogType = actualTemplate.LogType
        };
        manager.ReplaceConfigured([], [redirectedTemplate]);

        try
        {
            var sink = new DataManagerFlowLogSink(manager);
            sink.Write(CreateRecord("packing warning", FlowLogLevel.Warning));

            var routedLog = manager["ProcessA\\FlowA"];
            Assert.NotNull(routedLog);
            Assert.Equal(
                Path.Combine(logDirectory, "ProcessA", "FlowA"),
                routedLog!.Param.FilePath);
            (routedLog as IDisposable)?.Dispose();
            AssertLogContains(logDirectory, "Warning", "packing warning");
        }
        finally
        {
            foreach (var log in manager.Items.OfType<IDisposable>())
                log.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>创建一条带完整宿主路由上下文的流程日志。</summary>
    private static FlowLogRecord CreateRecord(string message, FlowLogLevel level)
        => new("ProcessA", "FlowA", "NodeA", 1, message, level);

    /// <summary>创建一条可指定流程与任务名称的宿主日志记录。</summary>
    private static FlowLogRecord CreateRecord(
        string message,
        FlowLogLevel level,
        string processName,
        string flowName)
        => new(processName, flowName, "NodeA", 1, message, level);

    /// <summary>取得动态日志目录相对于 FlowLog 根目录的路径段。</summary>
    private static string[] GetRelativeSegments(string root, string path)
        => Path.GetRelativePath(root, path)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>按名注册基础 FlowLog，等价于宿主 LogConfigService 装载配置后的状态。</summary>
    private static void RegisterBaseFlowLog(LogManager manager, string logDirectory)
        => manager.ReplaceConfigured([], [CreateTemplate(logDirectory)]);

    /// <summary>创建只包含 FlowLog 基础实例的日志模板。</summary>
    private static LogParam CreateTemplate(string logDirectory)
        => new()
        {
            Name = "FlowLog",
            FilePath = logDirectory,
            Separator = "||",
            SaveDays = 7,
            LogType = ELogType.SeriLog,
        };

    /// <summary>验证 DataManager 实际文件同时保留等级与消息。</summary>
    private static void AssertLogContains(string root, string level, string message)
    {
        var file = Directory.GetFiles(root, "log*", SearchOption.AllDirectories).Single();
        var contents = File.ReadAllText(file);
        Assert.Contains(level, contents, StringComparison.Ordinal);
        Assert.Contains(message, contents, StringComparison.Ordinal);
    }

    /// <summary>从测试输出目录向上定位 Packing Machine 的实际日志配置。</summary>
    private static string FindMachineAppLogConfig()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "MachineApp",
                "MachineApp",
                "Config",
                "LogConfig.json");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("无法定位 Packing Machine/Config/LogConfig.json。");
    }
}
