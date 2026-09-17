using DataManager.Log;
using DataManager.Log.Definition;
using FlowEditor.Core;
using System.Diagnostics;
using FlowEditor.DataManagerLogging;
using Xunit;

namespace WPFControl.Host.Tests;

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
}
