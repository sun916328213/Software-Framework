// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DapperCommandFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、Dapper、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.Data;
using System.Threading;

namespace DataManager.Runtime.SQL.Dapper
{
    /// <summary>
    /// P9-3/P9-4：Dapper 命令构造器（统一携带命令超时与取消标记）。
    /// 只有走 Dapper 原生 <see cref="CommandDefinition"/> 的执行路径才能真正传递取消与超时；
    /// 短连接的 <c>Open()</c>、以及 Dommel 不支持 Token 的少数入口属第三方同步/扩展调用，
    /// 边界明确——不伪装成「已取消」。
    /// </summary>
    public static class DapperCommandFactory
    {
        /// <summary>
        /// 构造命令定义：<paramref name="sqlParam"/> 非空时把 <see cref="SQLParam.CommandTimeoutOrNull"/>
        /// 映射为 Dapper 的 commandTimeout（null = provider 默认，即 SQLParam.CommandTimeoutSeconds 为 0）。
        /// </summary>
        public static CommandDefinition Create(
            string sql,
            object? parameters = null,
            IDbTransaction? transaction = null,
            SQLParam? sqlParam = null,
            CancellationToken cancellationToken = default) =>
            new(sql, parameters, transaction,
                commandTimeout: sqlParam?.CommandTimeoutOrNull,
                cancellationToken: cancellationToken);
    }
}
