# DataManager

统一的数据 access 入口：日志（Serilog / NLog / log4net）、Excel（EPPlus / MiniExcel）、数据库（EFCore / SqlSugar / Dapper，支持 PostgreSQL / MySQL / SQLite / SQL Server），外加测试工程与 WPF 测试工作台。

- 构建：`dotnet build DataManager.sln`（0 错误；警告只来自同仓 Extension/WPFCustomControls 既有文件）
- 测试：`dotnet test DataManager.Tests/DataManager.Tests.csproj` + `dotnet test DataManager.Workbench.Tests/DataManager.Workbench.Tests.csproj`（377 用例，真库用例需本机数据库，见“测试”一节）

## 目录结构

```
DataManager/
  DataManager/            # 类库（net9.0-windows）
    Manager/              # ManagerBase<T,TParam> + IManager（实例注册/重名检查/TryGet）
    Excel/                # IExcel（= IExcelReader + IExcelWriter）/ ExcelManager
      EPPlus/             # 全功能：读写、追加、更新
      MiniExcel/          # 仅 Write/Query（追加/更新抛 NotSupportedException）
    Log/                  # ILog / LogManager / LogBase（查询解析）
      SeriLog/ NLog/ Log4Net/
    SQL/                  # ISQL<T> / SQLManager（无容器依赖，Create<T> 工厂）
      Dapper/ EFCore/ SqlSugar/
    DataManagerModule.cs  # Prism 模块（注册三个 Manager 单例）
  DataManager.Controls/   # 可复用 WPF 控件层：SqlWorkspace<T> + SqlViewModel<T> 等
  DataManager.Tests/      # xUnit（单元 + 真库集成）
  DataManager.Workbench.Tests/ # xUnit（无头驱动工作台控件/VM；WPF 需 STA）
  DataManager.Workbench/  # WPF + Prism.DryIoc + .NET 9 手动测试台
```

## 引用

```xml
<ProjectReference Include="...\DataManager\DataManager\DataManager.csproj" />
```

主要包（类库自带）：`Dapper` + `Dommel`、`EPPlus`、`MiniExcel`、`NLog`、`log4net`、`Serilog(.Sinks.File)`、`Npgsql.EntityFrameworkCore.PostgreSQL`、`MySql.EntityFrameworkCore`、`SqlSugarCore`。

### 框架基础依赖（设计前提，不做隔离层）

`DataManager.csproj` 直接引用两个同仓基础库，它们是**必需依赖**，不列入待解耦对象：

- `Prism`（`Prism.Core` / `Prism.Wpf` / `Prism.DryIoc`）：`DataManagerModule : IModule` 本身就是模块契约；移除等于放弃模块化入口。
- `Extension`（`net9.0-windows7.0` + WPF）：主题（`ThemeService`）、配置/JSON 读写、`DispatcherEx` 等共享基础设施。

因此 DataManager 的 TFM 与 Extension 保持一致（`net9.0-windows`），**不追求改成纯 `net9.0`**（Extension 自带 WPF 目标框架，改不掉）。这两条依赖由守卫测试 `Tests/SQL/SqlDependencyArchitectureTests.cs` 的 `Prism_RemainsDirectlyReferenced` / `Extension_RemainsDirectlyReferenced` 锁定，避免被后续重构误删。

## Log：三实现同一文件格式

```csharp
var mgr = new LogManager();
var log = mgr.Create(new LogParam
{
    Name = "app", FilePath = @"C:\logs", Separator = "||",
    SaveDays = 180, LogType = ELogType.SeriLog, // 或 NLog / Log4Net
});
log.Add("hello", ELogLevel.Information);
await log.AddAsync("hello", ELogLevel.Warning);

var today = log.Query(DateTime.Today, DateTime.Today); // 按时间范围
var lines = log.Query_Multi("log20260907");            // 多行原文
```

- 行格式三实现一致：`yyyy/MM/dd HH:mm:ss || 级别 || 内容`，按天滚动为 `logyyyyMMdd`（无扩展名），`LogBase` 的查询对三者通用。
- 常驻写：Serilog（单 Logger + LogContext 传级别）、NLog（独占 LogFactory）、log4net（独占 Repository + MinimalLock），`Dispose` 释放句柄，读文件用共享读。
- 级别映射：`Information→Info`，`Warning/Reset→Warn`，`Error→Error`，`SysError→Fatal`。

## Excel：按能力选型

```csharp
var mgr = new ExcelManager();
var excel = mgr.Create(new ExcelParam
{
    Name = "report", FilePath = @"C:\excel", FileName = "r.xlsx",
    ToolType = ExcelType.EPPlus, // 全功能
});

excel.Write(list);                 // 覆盖写
excel.Add(item); excel.AddRange(list);   // 追加（仅 EPPlus）
var all = excel.Query<Person>();
var old = excel.Query<Person>(x => x.Age > 25);
excel.Update<Person>(x => x.Name == "A", new Person { ... }); // 仅 EPPlus
```

| 能力 | EPPlus | MiniExcel |
|---|---|---|
| Write/Query（含条件/异步） | ✓ | ✓ |
| Add/AddRange（追加） | ✓ | ✗（抛 `NotSupportedException`） |
| Update | ✓ | ✗ |
| 缺文件 Query | 返回空 | 返回空 |

只读场景请依赖 `IExcelReader`，只写场景请依赖 `IExcelWriter`。

## SQL：三工具统一 `ISQL<T>`

```csharp
var mgr = new SQLManager(); // 无容器依赖；需进 IOC 时用 Create 的 onCreated 回调自行注册
var db = mgr.Create<MyEntity>(new SQLParam
{
    Name = "main", ServerIP = "127.0.0.1", Port = "5432",
    UserName = "postgres", Password = "123", DBName = "mydb",
    DBType = ESQLType.PostgreSQL, ToolType = ESQLToolType.Dapper, // 或 EFCore / SqlSugar
});
db.Connect(); // 建库 + 建表（EF：库存在也会补建缺失表；Dapper：短连接，用完即放）

db.Add(item); db.AddRange(list);
var all = db.Query(); var one = db.Query(x => x.Age > 20);
db.Update(x => x.Id == 1, new MyEntity { Name = "B", Age = 21 }); // Dapper/EFCore：跳过 [Key]，null 不覆盖；SqlSugar 按原生语义全列更新
db.Update(item); // P9-3 单实体按主键（三路对齐；EFCore 整行语义 null 会覆盖）
db.Delete(x => x.Id == 1); db.DeleteAll();
db.DisConnect();
```

```csharp
// P10 事务：单实例同连接原子提交，异常回滚
db.ExecuteInTransaction(() =>
{
    db.Add(a); db.Add(b); db.Update(c);
});
// 矩阵：非分表三路全原子；Dapper/SqlSugar 分表同连接原子；
// EFCore 分表抛（fork 独立连接）；嵌套抛。
```

```csharp
// P9 取消：每个异步入口都有 CancellationToken 重载；旧无参签名保留，
// 内部等价于传 CancellationToken.None（源码兼容，不必改既有调用点）。
using var cts = new CancellationTokenSource();
var token = cts.Token;

await db.QueryAsync(token);
await db.QueryAsync(x => x.Age > 20, token);
await db.AddAsync(item, token);
await db.AddRangeAsync(list, token);
await db.UpdateAsync(item, token);
await db.UpdateAsync(x => x.Id == 1, new MyEntity { ... }, token);
await db.DeleteAsync(x => x.Id == 1, token);
await db.DeleteAllAsync(token);
await db.ExecuteInTransactionAsync(async () =>
{
    await db.AddAsync(a, token);
    await db.AddAsync(b, token);
}, token);
// 取消语义（三 provider 对齐）：取消向上抛 OperationCanceledException——不吞掉、
// 不返回空集合/false；事务内取消与异常一样整体回滚（回滚本身不受取消影响）。
// Token 只走参数，不进 SQLParam（SQLParam 是连接配置，不含每次调用的运行时状态）。
```

```csharp
// P9 命令超时：SQLParam.CommandTimeoutSeconds（秒）。默认 0 = 沿用 provider 默认，现场行为不变。
var db2 = mgr.Create<MyEntity>(new SQLParam
{
    Name = "main", ServerIP = "127.0.0.1", Port = "5432", DBName = "mydb",
    DBType = ESQLType.PostgreSQL, ToolType = ESQLToolType.Dapper,
    CommandTimeoutSeconds = 30,
});
// 映射：Dapper → CommandDefinition.commandTimeout（0 → null）；
//       EFCore → UseXxx(conn, o => o.CommandTimeout(...))（构造期固化 ⇒ 改值触发实例重建）；
//       SqlSugar → IAdo.CommandTimeOut。
// 负值由 SQLParam.Validate() 直接拒绝。
```

- 取消的 provider 边界（非本库可保证，已用测试显式锁住）：
  - SqlSugar **分表** provider（`SplitInsertable` / `SplitTableUpdateByObjectProvider` / `SplitTableDeleteByObjectProvider`）无 Token 重载 ⇒ 只做「进入前检查」，不假装可中断。
  - `Microsoft.Data.Sqlite` 在**库级锁等待**中不响应取消，按连接串 `Default Timeout` 超时后抛 `SqliteException`（SQLITE_BUSY）而非 `OperationCanceledException`。
- 工作台侧：`SqlWorkspace` 同一时刻至多一个在途操作（新操作接管时先取消旧的），重连/切换档案/断开/释放都会先取消在途操作；释放后不再接受新操作、也不再回写结果集。`SqlViewModel.CancelInFlight()` 是宿主的取消入口。
- 时间：全链路用 `DateTime.Now/Today`（Local）；Npgsql 兼容开关由库内静态构造自行保证，宿主无需设置。
- Dapper 命名约定：表/列一律小写单数（`[Table]/[Column]` 名同样小写化），已注册全局 Dommel 解析器对齐，手写 SQL 与 Dommel 一致。
- `SQLParam` 有 `Validate()`（按库分支校验，空值抛 `ArgumentException`），`Create` 入口自动校验；`Manager` 重名抛 `InvalidOperationException`。
- `ISQL` 参数与状态只读（构造期注入），`Dispose` 释放底层连接（EFCore 继承 DbContext；SqlSugar 关 client；Dapper 短连接仅置状态位）。
- 方言策略：`ISqlDialect/SqlDialectRegistry`（PG/MySQL/SQLite/SQL Server），加库实现接口+注册即可，不再散落 switch。

```csharp
// SQLite：文件自建自清，无服务器
var lite = mgr.Create<MyEntity>(new SQLParam
{
    Name = "lite", FilePath = @"C:\data\app.db", // 或 DBName 当路径
    DBType = ESQLType.SQLite, ToolType = ESQLToolType.Dapper,
});
// SQL Server：端口可选，默认信任服务器证书
var ms = mgr.Create<MyEntity>(new SQLParam
{
    Name = "ms", ServerIP = "localhost", DBName = "app",
    UserName = "sa", Password = "pwd", TrustServerCertificate = true,
    DBType = ESQLType.SQL_Server, ToolType = ESQLToolType.EFCore,
});
// 直通串（优先级最高，分字段可空）：
// new SQLParam { Name = "x", DBType = ESQLType.SQL_Server, ConnectionString = "Server=...;..." }
```

## 分表（按天/月/年，自动路由）

```csharp
[DbSplitTable(SplitPeriod.Day)]   // 新代码用共享特性；Dapper 旧特性、SqlSugar 原生特性同样识别
public class MyLog
{
    [Key] public long Id { get; set; }
    [DbSplitField]                // 不标则取首个 DateTime 属性
    public DateTime CreateTime { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

- 表名规则：`[Table] 或类名 + _yyyyMMdd/_yyyyMM/_yyyy`；写入按实体日期自动路由建表，查询/更新/删除逐表。
- 条件查询下推：EFCore/SqlSugar 逐表服务端过滤；Dapper 比较/与或非/null/bool/枚举谓词同样下推（`DapperPredicateTranslator`，全参数化），不可译（方法调用/算术等）自动回退内存过滤；谓词含分表字段等值/有界范围约束时仅查命中表（日期裁剪，500 槽熔断；范围/OR 全表，正确优先；勿更新分表字段本身），大表慎用。
- EFCore：`IsSplitTableEntity/GetSplitTableName/ForkForTable/GetSplitTables` 公开，`Connect()` 建当期表，写前自动建目标期表。
- SqlSugar：原生 `[SugarTable("t_{year}{month}{day}")]/[SplitTable]/[SplitField]` 同样直通；原生约束：分表主键禁自增（跨表撞号），须显式赋值；`GetSplitTables()` 三路对齐（工作台分表命令全支持）。
- 注意：EF 沿用默认映射（不做 PG 小写化），同一实体在 EF 与 Dapper 下的物理表名大小写可能不同，各管各的表，行为一致。

## 测试

```powershell
# 单元（无需数据库）
dotnet test DataManager.Tests/DataManager.Tests.csproj
dotnet test DataManager.Workbench.Tests/DataManager.Workbench.Tests.csproj
# 含 PG 真库（需本机 PostgreSQL，先设密码）
$env:DM_PG_PASSWORD="123"
dotnet test DataManager.Tests/DataManager.Tests.csproj
```

- 连接信息：`DM_PG_HOST/DM_PG_PORT/DM_PG_USER/DM_PG_PASSWORD/DM_PG_DB`，默认 `127.0.0.1:5432/postgres/dm_integration`，库表自建自清。**无本机真库时失败的都是集成用例**（DataManager.Tests 22 条：PG 10 + MySQL 6 + SQL Server 6；Workbench.Tests 2 条：PG 冒烟），属环境问题，非回归；SQLite 真库文件自建自清，CI 可跑。
- 377 用例构成：DataManager.Tests 310（Excel / Log / Manager / SQL 单元 + 真库集成）+ DataManager.Workbench.Tests 67（工作台控件与 VM 无头驱动）。
- P9 新增 39 条：`SQL/SqlAsyncApiCompatTests` 4（旧签名冻结）+ `SQL/SqlDependencyArchitectureTests` 6（Prism/Extension 基础依赖 + AnyCPU）+ `SQL/SqlCancellationTests` 12（取消契约与事务回滚）+ `SQL/SqlCommandTimeoutTests` 8（超时映射）+ `Workbench.Tests/SqlViewModelCancellationTests` 9（工作台取消与退出安全）。
- 工作台冒烟（`Tests/Workbench/SqlViewModelSmokeTests`）无头驱动 `SqlViewModel` 全命令连真库；`LogViewModelRefreshTests` 端到端验证刷新能找到 `logyyyyMMdd` 文件。

## 工作台（手动测试，MaterialDesign + Prism）

```powershell
dotnet run --project DataManager.Workbench/DataManager.Workbench.csproj
# 或设为启动项目 F5
```

三页（SQL / 日志 / Excel）全部经 `*Manager.Create` 走 DataManager 方法，无旁路。改连接/路径/文件名后直接操作即可，实例按“全部要素指纹”自动重建（含密码纠错场景）；SQL 页默认连 `localhost:5432/postgres/workbench`。
