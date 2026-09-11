using DataManager.Log;
using DataManager.Log.Definition;
using Extension.Struct;
using System.IO;

namespace DataManager.Workbench.Services
{
    /// <summary>
    /// 日志实际流程（ViewModel 只剩绑定与命令装配）。
    /// 职责：日志实例生命周期（指纹重建）、写入、文件枚举、查询；
    /// 不碰 UI：计时/日志输出/结果集全部由 ViewModel 包装完成。
    /// </summary>
    public sealed class LogWorkspace
    {
        /// <summary>日志文件名前缀（logyyyyMMdd，无扩展名，三种实现一致）。</summary>
        public const string LogFilePattern = "log*";

        private readonly LogManager _logManager;
        private readonly Random _random = new();
        private ILog? _log;
        private string? _currentKey;

        public LogWorkspace(LogManager logManager)
        {
            _logManager = logManager;
        }

        /// <summary>实例指纹：全部配置要素参与，任一改动都重建实例。</summary>
        public static string BuildLogKey(LogParam param) =>
            $"{param.LogType}:{param.Name}:{param.FilePath}:{param.Separator}:{param.SaveDays}";

        /// <summary>强制重建实例（初始化命令用）。</summary>
        public ILog ResetLog(LogParam param)
        {
            if (_log != null)
            {
                try { (_log as IDisposable)?.Dispose(); } catch { }
                _logManager.Remove(_log);
                _log = null;
            }
            _log = _logManager.Create(param);
            _currentKey = BuildLogKey(param);
            return _log;
        }

        /// <summary>按指纹确保实例（命中复用，否则重建；返回是否发生重建）。</summary>
        public (ILog Log, bool Rebuilt) EnsureLog(LogParam param)
        {
            var key = BuildLogKey(param);
            if (_log != null && _currentKey == key)
                return (_log, false);
            var changed = _log != null;
            var log = ResetLog(param);
            return (log, changed);
        }

        public async Task WriteAsync(LogParam param, string message, ELogLevel level)
        {
            var (log, _) = EnsureLog(param);
            await log.AddAsync(message, level);
        }

        public async Task WriteManyAsync(LogParam param, int count = 100)
        {
            var (log, _) = EnsureLog(param);
            for (int i = 1; i <= count; i++)
            {
                await log.AddAsync($"第 {i} 条批量测试日志 - {_random.Next(1000, 9999)}", (ELogLevel)(i % 5));
            }
        }

        public IReadOnlyList<string> ListLogFiles(string dir) =>
            Directory.Exists(dir)
                ? Directory.GetFiles(dir, LogFilePattern).Select(f => Path.GetFileName(f)!).ToList()
                : new List<string>();

        public Task<IReadOnlyList<LogInfo>> QueryByFileAsync(LogParam param, string file) =>
            Task.Run(() =>
            {
                var (log, _) = EnsureLog(param);
                return (IReadOnlyList<LogInfo>)log.Query(file).ToList();
            });

        public Task<IReadOnlyList<LogInfo>> QueryByRangeAsync(LogParam param, DateTime start, DateTime end) =>
            Task.Run(() =>
            {
                var (log, _) = EnsureLog(param);
                return (IReadOnlyList<LogInfo>)log.Query(start, end).ToList();
            });

        public Task<IReadOnlyList<string>> QueryMultiAsync(LogParam param, string file) =>
            Task.Run(() =>
            {
                var (log, _) = EnsureLog(param);
                return (IReadOnlyList<string>)log.Query_Multi(file).ToList();
            });
    }
}
