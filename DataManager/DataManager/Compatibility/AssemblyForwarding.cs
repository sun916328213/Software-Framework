// P12 兼容门面：把已迁入 DataManager.Core / DataManager.Runtime 的公开类型转发出去，
// 已编译的外部调用方（旧 DataManager.dll 引用）继续在运行时解析到新程序集。
// 新代码：contract/model → DataManager.Core；create provider → DataManager.Runtime。
// 门面不再承载 Provider 实现。
using System.Runtime.CompilerServices;

// ---- Core：管理器契约 ----
[assembly: TypeForwardedTo(typeof(DataManager.IManager<,>))]
[assembly: TypeForwardedTo(typeof(DataManager.ManagerBase<,>))]

// ---- Core：SQL 契约 ----
[assembly: TypeForwardedTo(typeof(DataManager.SQL.ISQL))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.ISQL<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.ISQLManager))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.ISqlProviderFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.ISplitTableNameProvider))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SQLParam))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.ESQLType))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.ESQLToolType))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.ISqlDialect))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SplitPeriod))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.DbSplitTableAttribute))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.DbSplitFieldAttribute))]

// ---- Core：日志契约 ----
[assembly: TypeForwardedTo(typeof(DataManager.Log.ILog))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.LogBase))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.ILogManager))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.ILogProviderFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.Definition.LogInfo))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.Definition.LogParam))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.Definition.ELogType))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.Definition.ELogLevel))]

// ---- Core：Excel 契约 ----
[assembly: TypeForwardedTo(typeof(DataManager.Excel.IExcel))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.IExcelReader))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.IExcelWriter))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.ExcelBase))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.IExcelManager))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.IExcelProviderFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.Definition.ExcelParam))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.Definition.ExcelType))]

// ---- Runtime：模块与工厂 ----
[assembly: TypeForwardedTo(typeof(DataManager.DataManagerModule))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.SqlProviderFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.LogProviderFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.ExcelProviderFactory))]

// ---- Runtime：Manager 实现 ----
[assembly: TypeForwardedTo(typeof(DataManager.SQL.SQLManager))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.LogManager))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.ExcelManager))]

// ---- Runtime：SQL Provider ----
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.DapperBase<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.DapperHelper<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.DapperConnectionFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.DapperCommandFactory))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.SplitType))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.DapperSplitTableAttribute))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Dapper.DapperSplitFieldAttribute))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.EFCore.EFCoreBase<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.EFCore.EFCoreHelper<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.SqlSugar.SqlSugarHelper<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.SqlSugarSQL.SqlSugarBase<>))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SqlDialectHelper))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SqlDialectRegistry))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SqlNamingHelper))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.PostgresDialect))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.MySqlDialect))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SqliteDialect))]
[assembly: TypeForwardedTo(typeof(DataManager.SQL.Definition.SqlServerDialect))]

// ---- Runtime：日志 Provider ----
[assembly: TypeForwardedTo(typeof(DataManager.Log.SeriLog.SerilogHelper))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.NLog.NLogHelper))]
[assembly: TypeForwardedTo(typeof(DataManager.Log.Log4Net.Log4NetHelper))]

// ---- Runtime：Excel Provider ----
[assembly: TypeForwardedTo(typeof(DataManager.Excel.EPPlus.EPPlusHelper))]
[assembly: TypeForwardedTo(typeof(DataManager.Excel.MiniExcel.MiniExcelHelper))]
