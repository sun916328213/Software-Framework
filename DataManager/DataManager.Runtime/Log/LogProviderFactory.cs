using DataManager.Log.Definition;
using DataManager.Log.Log4Net;
using DataManager.Log.NLog;
using DataManager.Log.SeriLog;

namespace DataManager.Log;

/// <summary>
/// P12 Runtime 内建日志 Provider 工厂：按日志类型创建具体日志（不负责注册）。
/// 新增日志后端在此注册/扩展，不修改 Controls。
/// </summary>
public sealed class LogProviderFactory : ILogProviderFactory
{
    /// <inheritdoc />
    public ILog Create(LogParam logParam)
    {
        ArgumentNullException.ThrowIfNull(logParam);
        logParam.Validate();

        return logParam.LogType switch
        {
            ELogType.SeriLog => new SerilogHelper { Param = logParam },
            ELogType.NLog => new NLogHelper { Param = logParam },
            ELogType.Log4Net => new Log4NetHelper { Param = logParam },
            _ => throw new NotSupportedException($"{logParam.LogType} not support."),
        };
    }
}
