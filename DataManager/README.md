# DataManager

统一的数据 access 入口：日志（Serilog / NLog / log4net）、Excel（EPPlus / MiniExcel）、数据库（EFCore / SqlSugar / Dapper，支持 PostgreSQL / MySQL），外加测试工程与 WPF 测试工作台。

- 构建：`dotnet build DataManager.sln`（0 警告 0 错误）
- 测试：`dotnet test DataManager.Tests/DataManager.Tests.csproj`（130 用例，需本机 PG + 见“测试”一节）

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
  DataManager.Tests/      # xUnit（单元 + PG 真库集成 + 工作台冒烟）
  DataManager.Workbench/  # WPF + Prism.DryIoc + .NET 9 手动测试台
```

## 引用

```xml
<ProjectReference Include="...\DataManager\DataManager\DataManager.csproj" />
```

主要包（类库自带）：`Dapper` + `Dommel`、`EPPlus`、`MiniExcel`、`NLog`、`log4net`、`Serilog(.Sinks.File)`、`Npgsql.EntityFrameworkCore.PostgreSQL`、`MySql.EntityFrameworkCore`、`SqlSugarCore`。

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
db.Delete(x => x.Id == 1); db.DeleteAll();
db.DisConnect();
```

- 时间：全链路用 `DateTime.Now/Today`（Local）；Npgsql 兼容开关由库内静态构造自行保证，宿主无需设置。
- Dapper 命名约定：表/列一律小写单数（`[Table]/[Column]` 名同样小写化），已注册全局 Dommel 解析器对齐，手写 SQL 与 Dommel 一致。
- `SQLParam` 有 `Validate()`（空值抛 `ArgumentException`），`Create` 入口自动校验；`Manager` 重名抛 `InvalidOperationException`。

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

- 表名规则：`[Table] 或类名 + _yyyyMMdd/_yyyyMM/_yyyy`；写入按实体日期自动路由建表，查询/更新/删除逐表（条件查询内存过滤，大表慎用）。
- EFCore：`IsSplitTableEntity/GetSplitTableName/ForkForTable/GetSplitTables` 公开，`Connect()` 建当期表，写前自动建目标期表。
- SqlSugar：原生 `[SugarTable("t_{year}{month}{day}")]/[SplitTable]/[SplitField]` 同样直通。
- 注意：EF 沿用默认映射（不做 PG 小写化），同一实体在 EF 与 Dapper 下的物理表名大小写可能不同，各管各的表，行为一致。

## 测试

```powershell
# 单元（无需数据库）
dotnet test DataManager.Tests/DataManager.Tests.csproj
# 含 PG 真库（需本机 PostgreSQL，先设密码）
$env:DM_PG_PASSWORD="123"
dotnet test DataManager.Tests/DataManager.Tests.csproj
```

- 连接信息：`DM_PG_HOST/DM_PG_PORT/DM_PG_USER/DM_PG_PASSWORD/DM_PG_DB`，默认 `127.0.0.1:5432/postgres/dm_integration`，库表自建自清。
- 130 用例构成：Excel 20（EPPlus 8 + MiniExcel 6 + Manager 6）/ Log 28（Serilog 8 + NLog 6 + log4net 6 + Manager 8）/ Manager 12 / SQL 50（EFCore 分表 6 + PG 真库 5 + 工厂/命名/方言等 39）/ Workbench 20（SQL 指纹 6 + Log/Excel 指纹 9 + 刷新 3 + 冒烟 2）。
- 工作台冒烟（`Tests/Workbench/SqlViewModelSmokeTests`）无头驱动 `SqlViewModel` 全命令连真库；`LogViewModelRefreshTests` 端到端验证刷新能找到 `logyyyyMMdd` 文件。

## 工作台（手动测试，MaterialDesign + Prism）

```powershell
dotnet run --project DataManager.Workbench/DataManager.Workbench.csproj
# 或设为启动项目 F5
```

三页（SQL / 日志 / Excel）全部经 `*Manager.Create` 走 DataManager 方法，无旁路。改连接/路径/文件名后直接操作即可，实例按“全部要素指纹”自动重建（含密码纠错场景）；SQL 页默认连 `localhost:5432/postgres/workbench`。
