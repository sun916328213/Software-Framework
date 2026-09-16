using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DataManager.Log;
using DataManager.Log.Definition;
using FlowEditor.Core;

namespace FlowEditor.DataManagerLogging;

/// <summary>
/// 将 FlowEditor 的结构化运行日志映射到 DataManager 动态日志实例。
/// 逻辑路由身份、物理路径清理、配置热重载和故障隔离均封装在本 adapter 内。
/// </summary>
public sealed class DataManagerFlowLogSink : IFlowLogSink
{
    private static readonly HashSet<string> ReservedPathNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private readonly LogManager _logManager;
    private readonly IFlowLogConfiguration _configuration;
    private readonly object _sync = new();
    private readonly HashSet<string> _dynamicLogKeys = new(StringComparer.Ordinal);
    private long _nextDiagnosticUtcTicks;
    private int _suppressedDiagnosticCount;

    /// <summary>创建 DataManager 流程日志 adapter。</summary>
    /// <param name="logManager">宿主共享的日志管理器。</param>
    /// <param name="configuration">当前 FlowLog 模板及配置变更通知提供者。</param>
    public DataManagerFlowLogSink(LogManager logManager, IFlowLogConfiguration configuration)
    {
        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configuration.ConfigurationChanged += OnConfigurationChanged;
    }

    /// <inheritdoc />
    public void Write(FlowLogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        try
        {
            var route = record.Route;
            if (route == null) return;
            lock (_sync)
            {
                var key = BuildManagerKey(route.Value);
                Resolve(key, route.Value.ProcessName, route.Value.FlowName)
                    ?.Add(record.Message, MapLevel(record.Level));
            }
        }
        catch (Exception ex)
        {
            try
            {
                ReportWriteFailure(ex);
            }
            catch
            {
                // 第三方 TraceListener 故障不得改变流程执行结果。
            }
        }
    }

    /// <summary>解析逻辑路由对应的日志实例；不存在时按流程和任务两个独立目录段创建。</summary>
    private ILog? Resolve(string logKey, string? processName, string flowName)
    {
        var existing = _logManager[logKey];
        if (existing != null) return existing;

        var template = _configuration.FlowLogTemplate;
        if (template == null) return null;

        var directory = template.FilePath;
        if (!string.IsNullOrEmpty(processName))
            directory = Path.Combine(directory, SanitizePathSegment(processName));
        directory = Path.Combine(directory, SanitizePathSegment(flowName));

        _logManager.Config([BuildParam(template, logKey, directory)]);
        _dynamicLogKeys.Add(logKey);
        return _logManager[logKey];
    }

    /// <summary>配置成功变更后释放动态实例，使后续写入按新模板重建。</summary>
    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            foreach (var key in _dynamicLogKeys)
            {
                var log = _logManager[key];
                if (log == null || !_logManager.Remove(log)) continue;
                (log as IDisposable)?.Dispose();
            }
            _dynamicLogKeys.Clear();
        }
    }

    /// <summary>基于基础模板创建动态日志参数。</summary>
    private static LogParam BuildParam(LogParam template, string name, string directory)
        => new()
        {
            Name = name,
            FilePath = directory,
            Separator = template.Separator,
            SaveDays = template.SaveDays,
            LogType = template.LogType,
        };

    /// <summary>显式映射 Runtime 与 DataManager 日志等级。</summary>
    private static ELogLevel MapLevel(FlowLogLevel level) => level switch
    {
        FlowLogLevel.Information => ELogLevel.Information,
        FlowLogLevel.Warning => ELogLevel.Warning,
        FlowLogLevel.Reset => ELogLevel.Reset,
        FlowLogLevel.Error => ELogLevel.Error,
        FlowLogLevel.SysError => ELogLevel.SysError,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "不支持的流程日志等级。"),
    };

    /// <summary>构造 DataManager 内部键，并隔离扁平键歧义或基础日志重名。</summary>
    private string BuildManagerKey(FlowLogRoute route)
    {
        var readableKey = route.DisplayKey;
        var conflictsWithBaseLog = _logManager[readableKey] != null &&
                                   !_dynamicLogKeys.Contains(readableKey);
        return route.HasAmbiguousDisplayKey || conflictsWithBaseLog
            ? $"{readableKey}|route:{route.StableId}"
            : readableKey;
    }

    /// <summary>将逻辑名称转换为安全、定长且抗清理碰撞的单一路径段。</summary>
    private static string SanitizePathSegment(string name)
    {
        const int maxSegmentLength = 96;
        var original = name;
        foreach (var character in Path.GetInvalidFileNameChars())
            name = name.Replace(character, '_');
        name = name.Replace('/', '_').Replace('\\', '_');
        name = name.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name)) name = "_";
        var stem = name.Split('.', 2)[0];
        if (ReservedPathNames.Contains(stem)) name = $"_{name}";

        var changed = !string.Equals(original, name, StringComparison.Ordinal);
        if (!changed && name.Length <= maxSegmentLength) return name;

        var suffix = $"~{BuildStableSuffix(original)}";
        var prefixLength = Math.Max(1, maxSegmentLength - suffix.Length);
        if (name.Length > prefixLength) name = name[..prefixLength];
        return $"{name}{suffix}";
    }

    /// <summary>以长度前缀编码输入并计算稳定短摘要。</summary>
    private static string BuildStableSuffix(params string?[] values)
    {
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            var safeValue = value ?? string.Empty;
            builder.Append(safeValue.Length).Append(':').Append(safeValue).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash.AsSpan(0, 6));
    }

    /// <summary>以三十秒限频的 Trace 诊断报告日志后端故障。</summary>
    private void ReportWriteFailure(Exception exception)
    {
        const long intervalTicks = TimeSpan.TicksPerSecond * 30;
        var now = DateTime.UtcNow.Ticks;
        var next = Volatile.Read(ref _nextDiagnosticUtcTicks);
        if (now < next || Interlocked.CompareExchange(
                ref _nextDiagnosticUtcTicks,
                now + intervalTicks,
                next) != next)
        {
            Interlocked.Increment(ref _suppressedDiagnosticCount);
            return;
        }

        var suppressed = Interlocked.Exchange(ref _suppressedDiagnosticCount, 0);
        var suffix = suppressed == 0 ? string.Empty : $"；期间已抑制 {suppressed} 条重复错误";
        Trace.TraceError($"[FlowLog] 宿主日志写入失败: {exception.Message}{suffix}");
    }
}
