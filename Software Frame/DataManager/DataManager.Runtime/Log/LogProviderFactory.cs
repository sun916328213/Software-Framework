// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogProviderFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Log、DataManager.Core.Log.Definition、DataManager.Runtime.Log.Log4Net、DataManager.Runtime.Log.NLog。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Runtime.Log.Log4Net;
using DataManager.Runtime.Log.NLog;
using DataManager.Runtime.Log.SeriLog;

namespace DataManager.Runtime.Log;

/// <summary>
/// P12 Runtime 内建日志 Provider 工厂：按日志类型创建具体日志（不负责注册）。
/// 新增日志后端在此注册/扩展，不修改 Controls。
/// </summary>
public sealed class LogProviderFactory : ILogProviderFactory
{
    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 ThrowIfNull、Validate；具体条件和先后次序见方法体。
    /// </remarks>
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
