# DataManager 优化计划（待改项存档，Codex 可直接执行）

> 生成日期：2026-09-07
> 仓库子目录：`DataManager/`
> 现状基线：`dotnet build DataManager.sln` 0 错误（DataManager 三工程 0 警告；Extension 旧警告与本库无关）；`dotnet test` 131/131 通过（需 `DM_PG_PASSWORD` 环境变量）。
> 本文档 = 上次架构评审的落盘版，后续 Codex/人工按 P0（已完成） → P1 → P2 顺序执行。

---

## 1. 架构现状速览

```
DataManager/
  Manager/ManagerBase.cs            # 通用管理器基类 <T, TParam>
  Excel/IExcel.cs, ExcelBase.cs, ExcelManager.cs
    Excel/EPPlus/EPPlusHelper.cs
    Excel/MiniExcel/MiniExcelHelper.cs
  Log/ILog.cs, LogBase.cs, LogManager.cs
    Log/SeriLog/SerilogHelper.cs
  SQL/ISQL.cs, SQLManager.cs
    SQL/Dapper/Core/DapperBase.cs(724行), DapperHelper.cs
    SQL/EFCore/Core/EFCoreBase.cs, EFCoreHelper.cs(Emit动态类型)
    SQL/SqlSugar/Core/SqlSugarBase.cs, SqlSugarHelper.cs
  DataManagerModule.cs              # Prism IModule 注册
DataManager.Tests/                  # xunit, 67 用例
DataManager.Workbench/              # WPF + Prism.DryIoc + .NET9 测试台
```

---

## 2. P0（行为修正，必须先做，约 1-2 天）—— ✅ 2026-09-07 已完成，70/70 绿

### P0-1 LogManager 返回 null 改抛异常 ✅
- 文件：`DataManager/Log/LogManager.cs:29-30`
- 现状：
  ```csharp
  case ELogType.NLog: return null;
  case ELogType.Log4Net: return null;
  ```
- 改为：
  ```csharp
  case ELogType.NLog:
  case ELogType.Log4Net:
      throw new NotSupportedException($"{logParam.LogType} not support yet. Only SeriLog.");
  ```
- 联动测试：`DataManager.Tests/Log/LogManagerTests.cs:37-45`
  - 现断言 `Assert.Null(manager.Create(param))` 需改为 `Assert.Throws<NotSupportedException>(...)`
- 联动 UI：`DataManager.Workbench/ViewModels/LogViewModel.cs:25-31` 已有 `仅支持 Serilog` 提示，改为捕获异常提示即可。
- 验收：`dotnet test` 全绿；Workbench 选 NLog 不崩溃，给出明确提示。

### P0-2 SQLManager 去 Prism 容器依赖（解耦 DIP）✅
- 落地：删除 `IContainerExtension` 字段/构造，改为无参 + `Action<object> onCreated` / `Create<T>(param, onCreated)` 回调；注册上移到上层。测试改为无容器 + 回调断言。
- 文件：`DataManager/SQL/SQLManager.cs:18-44`
- 现状：构造函数依赖 `IContainerExtension`，`Create<T>` 内 `ContainerRegistry.RegisterInstance(typeof(ISQL<T>), sql)`。
- 改为：
  1. 删除 `IContainerExtension` 字段和构造参数，改为无参 + 可选 `Action<ISQL<T>> onCreated` 回调。
  2. `Create<T>` 只 `DataManagers.Add(sql)` 并 `return sql`，不再注册容器。
  3. 容器注册上移到 `DataManager/DataManagerModule.cs:26-30` 或 Workbench `App.xaml.cs`，需要时由上层显式 `RegisterInstance`。
- 联动测试：`DataManager.Tests/SQL/SQLManagerTests.cs:36-102`
  - `new SQLManager(container)` 改为 `new SQLManager()`；
  - `Create_RegistersInstanceInContainer` 用例删除或改为验证 `onCreated` 回调被调用。
- 验收：DataManager 不再引用 `Prism.Ioc`（检查 csproj 可去 `Prism` 依赖或仅 Module 保留）；单测不再需要 `DryIocContainerExtension`。

### P0-3 Serilog 常驻 Logger（性能+多实例正确性）✅
- 落地：单实例常驻 `Logger` + `LogContext.PushProperty(CustomLevel)` 按事件写级别；`Param` 变更释放重建；`shared:true`；`LogBase` 读改 `FileShare.ReadWrite` 共享读；测试改共享读 + `using` + 新增 `Dispose_ReleasesFileLock`。
- 文件：`DataManager/Log/SeriLog/SerilogHelper.cs:31-79`
- 现状：`static Log.Logger` + 每次 `Add` 都 `Connect→Write→CloseAndFlush`；路径写死 `Path.Combine(Param.FilePath,"log")`。
- 改为：
  1. 每个 `SerilogHelper` 持有一个 `private Serilog.Core.Logger _logger;`，在 `Param` 设置或首次 `Add` 时 `EnsureLogger()` 懒加载创建。
  2. `Add` 只 `_logger.Information(info)`，不再开关全局 Logger；`Dispose/DisConnect` 才 `CloseAndFlush`。
  3. 路径规则明确：`Path.Combine(Param.FilePath, "log-.txt")` + `rollingInterval:Day` + `retainedFileCountLimit:SaveDays`，与 `LogBase.Query` 的读路径保持一致（重点核对 `LogBase.cs:26,81`）。
  4. 保留 `static object` 锁或改用实例锁；`CustomLevelEnricher` 保持。
- 联动测试：`DataManager.Tests/Log/SerilogHelperTests.cs` 追加 `Add_100次_文件数==预期且无串文件` 并发用例。
- 验收：Workbench `LogViewModel.WriteManyCommand` 100 条秒级完成；多 `LogManager.Create` 不同 `FilePath` 互不覆盖。

### P0-4 DapperBase 拆分 + 短连接（线程安全）✅
- 落地：新增 `DapperConnectionFactory`；删除 `_connection` 缓存，全 CRUD `using CreateOpenConnection()`；修 `InsertToTableAsync/DeleteAllAsync` 表名引用；去 `Npgsql/MySqlConnector` 直接 using。
- 文件：`DataManager/SQL/Dapper/Core/DapperBase.cs` 全文件（724行）
- 改为：
  1. 新建 `DapperConnectionFactory`（负责 `BuildConnectionString/CreateConnection`），`DapperSqlBuilder`（负责 `GenerateCreateTableSql/BuildCheckTableExists`），`DapperSplitRouter`（负责 `IsSplit/GetSplitTableName/GetSplitTables`）。
  2. 删除 `_connection` 缓存字段，所有 CRUD 用 `using var conn = _factory.CreateOpen();` 短连接。
  3. `DeleteAll/DeleteAllAsync` 表名加 `WrapTableName`，与 `GenerateCreateTableSql` 一致。
  4. 分表 `Query(predicate)` 先注明性能风险，短期保留内存过滤，长期加表达式转 SQL（列入 P1-4）。
- 验收：多线程 `AddAsync` 无 `Connection already open`；现有 SQL 单测全绿。

---

## 3. P1（设计+性能，约 1 周）—— ✅ 2026-09-07 已完成，78/78 绿

### P1-1 ManagerBase 封装 ✅
- 落地：新增 `Manager/IManager.cs`；`ManagerBase` 实现 `Items/TryGet/Contains`，`this[string]` 返回 `T?`，`Add` 重名抛 `InvalidOperationException`，`Remove` 返回 `bool`；三处 `Create` 改 `Add` + 前置重名检查。单测新增重名/TryGet/快照/Remove返回值。
- 文件：`DataManager/Manager/ManagerBase.cs:14,58,66`
- 改：`public readonly ConcurrentList<T> DataManagers` → `private` + `IReadOnlyList<T> Items`；`this[string]` 返回 `T?` 并加 `TryGet(name, out T?)`；`Create` 去重名检查（重名抛 `InvalidOperationException`）；`Config` 先参数校验；新建 `IManager<T,TParam>` 接口让三个 Manager 实现。
- 验收：Workbench `EnsureDb/EnsureLog/EnsureExcel` 改用 `TryGet`；单测加 `重复Name抛异常` 用例。

### P1-2 EFCore 去 Emit ✅
- 落地：`EFCoreHelper` 删除 `ModuleBuilder/Emit`，改为普通 `new`；闭合泛型已隔离模型缓存。测试 `IsType` 替代 `IsAssignableFrom`。
- 文件：`DataManager/SQL/EFCore/Core/EFCoreHelper.cs:22-55`，`EFCoreBase.cs:14-43`
- 改：删除 `ModuleBuilder/Emit`，`EFCoreBase<T>` 改为接受 `DbContextOptions` 构造；`EFCoreHelper<T>.Create(param)` 改为 `new EFCoreHelper<T>{SQLParam=param}` 普通 new；如需多实例隔离，用 `IDbContextFactory` 或每个 `SQLParam` 一个 `DbContextOptions` 缓存。
- 联动：`SQLManagerTests.cs:28-34` 的 `public WorkbenchEntity` 可见性注释可删除。
- 验收：删除 `System.Reflection.Emit` 引用后测试仍绿。

### P1-3 EFCore 语义修正 ✅
- 落地：`IsConnected` 去 setter 副作用；`Delete/Update` 改全匹配批量；`Update` 跳 `[Key]` + null；`DeleteAll` 改 `ExecuteDelete/Async`；全异步改 `ToListAsync/SaveChangesAsync`，`Query` 返回 `ToList` 快照；新增 `SaveChangesOptimizedAsync`。
- 文件：`DataManager/SQL/EFCore/Core/EFCoreBase.cs:55-76,153-168`
- 改：`IsConnected` 改为只读（`Connect()` 设置）；`Update` 只更新 `updatedata` 非空属性（参考 Dapper 的做法）；`DeleteAsync/QueryAsync/UpdateAsync` 改为真异步（`FirstOrDefaultAsync/ToListAsync/SaveChangesAsync + CancellationToken`）；`DeleteAll` 改 `ExecuteDeleteAsync`。
- 验收：无 `Task.Run` 包裹 EF 同步代码。

### P1-4 IExcel 拆分（ISP）✅
- 落地：新增 `IExcelReader/IExcelWriter`，`IExcel` 组合保持兼容；MiniExcel 不支持方法改 `NotSupportedException` 明示；单测新增抛异常 + Reader 视角用例。
- 文件：`DataManager/Excel/IExcel.cs`，`DataManager/Excel/MiniExcel/MiniExcelHelper.cs:42-59`
- 改：拆 `IExcelReader(Query)` / `IExcelWriter(Write)`，`IExcel : IReader,IWriter` 保持兼容；`MiniExcelHelper.Add/AddRange/Update` 明确抛 `NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新")` 并写 XML 注释；Workbench `ExcelViewModel.cs:106` 的 `IsEPPlus` 门控保留。
- 验收：单测显式断言 MiniExcel `Add` 抛 `NotSupportedException` 而非 `NotImplementedException`。

### P1-5 EPPlus 去状态化 + 类型转换 ✅
- 落地：`ExcelPackage/CurrentWorksheet` 标 `[Obsolete]`；`AddRange` 改 `GetLastUsedRow()+1`；`PropertyInfo` 加 `ConcurrentDictionary` 缓存；`CreateItem` 改 `SafeConvert` 支持 Nullable/Enum/DateTime/Guid/bool；单测新增 Nullable+枚举+日期往返。
- 文件：`DataManager/Excel/EPPlus/EPPlusHelper.cs:19-20,186-238,343-365`
- 改：删除 `ExcelPackage/CurrentWorksheet` 字段，`OpenExcel/SaveExcel/CloseExcel/SetCellStyle/AddChart` 标记 `[Obsolete]` 或移到独立 `EPPlusAdvanced` 类；`PropertyInfo[]` 加 `ConcurrentDictionary<Type,PropertyInfo[]>` 缓存；`CreateItem` 的 `Convert.ChangeType` 改为支持 `Nullable<T>/Enum/DateTime` 的 `SafeConvert`。
- 验收：并发 `Write+Query` 无 `ObjectDisposed`；枚举实体往返测试通过。

---

## 4. P2（工程标准化）—— ✅ 已完成，99/99 绿

- [x] TFM：三 csproj `net9.0-windows7.0` → `net9.0-windows`；删除 `LangVersion 13.0`（SDK 默认即 C#13）。
- [x] 包：删除未使用的 `Dapper.Sharding`、`Microsoft.Extensions.DependencyInjection.Abstractions/Options`（全仓无引用）；`Dommel` 保留（DapperBase 用 `Insert/Update/Delete/Select/GetAll`）；`MySql.EntityFrameworkCore 10.0.1` 保留（EFCoreBase 用 `UseMySQL`）。另补回此前丢失的 `log4net 2.0.17` 引用（构建曾因此失败，已恢复；有 NU1902 漏洞警告，后续评估升级）。
- [x] Param 校验：`ExcelParam/LogParam/SQLParam` 新增 `Validate()`（空值抛 `ArgumentException`，不用 `required` 以免破坏 `new Param()` 存量写法）；三处 `Create` 入口调用；`MiniExcel` 写前建目录、查缺失文件返回空（与 EPPlus 一致）。
- [x] 死目录：删除 csproj 中 `Excel\NPOI`（空）、`SQL\Dapper` 占位 `Folder`（与实际子目录无关）；`DataManagerModule` Npgsql 开关抽为 `ApplyNpgsqlLegacyTimestampBehavior()` 显式方法 + XML 文档（行为保留，需 Local 时间写入）。
- [x] 并发/释放：新增 `SerilogHelperTests.Add_ParallelWrites_AllPersisted`（50 并发）、`ManagerBaseTests.Add_ParallelUniqueNames_AllAdded`（100 并发）；校验/重名用例 6 个；真库 Testcontainers 未做（需 Docker，延期）。
- [x] Workbench 跨线程：新增 `Infrastructure/UiThread.cs`（有 Dispatcher 则封送，无则直行）；三 ViewModel 的 `Log()`、Log 页查询赋值、SQL 清空、Excel 查询赋值全部经 `UiThread.Invoke`。

---

## 5. 测试程序存档

- 位置：`DataManager.Tests/DataManager.Tests.csproj`（xunit 2.9.2）。
- 用例分布（131 个）：Excel 20 / Log 28 / Manager 12 / SQL 50 / Workbench 21（SQL 指纹 6 + Log/Excel 指纹 9 + 刷新 3 + 冒烟 2 + XAML 启动 1），TDD 证据同下。
- TDD 证据：`Tests/Excel/EPPlusHelperTests.cs` 追加覆盖回归；`Tests/Log/LogManagerTests.cs` 曾把坏味道显式断言（P0 已转正）。
- 运行：`dotnet test DataManager.Tests/DataManager.Tests.csproj --nologo -v minimal`

## 6. 工作台存档

- 位置：`DataManager.Workbench/DataManager.Workbench.csproj`（`UseWPF + Prism.DryIoc 8.1.97`，`net9.0-windows`）。
- 架构：`App.xaml.cs:PrismApplication + AddModule<DataManagerModule>`；`MainWindow.xaml:RegionMain` + `MainWindowViewModel.RequestNavigate`；`Sql/Log/ExcelView(+ViewModel)` + `Models/TestEntities` + `Styles/Theme.xaml` + `Infrastructure/UiThread`（后台线程回封送）。
- 方法全走 DataManager：Sql→`SQLManager.Create<T>/Connect/Add/Query/Update/Delete`；Log→`LogManager.Create/AddAsync/Query/Query_Multi`（Serilog/NLog/log4net 三切）；Excel→`ExcelManager.Create/Write/Add/Query/Update`。
- 运行：设为启动项目 F5 或 `dotnet run --project DataManager.Workbench`。

## 8. 警告清理记录（收尾）
- log4net `2.0.17` → `3.3.0`：NU1902（GHSA-4f7c-pmjv-c25w，XmlLayout 未过滤非法字符致丢日志）消除；经查我们只用 `RollingFileAppender + PatternLayout`，实际不受影响，升级后构建/测试全绿，API 兼容。
- 可空警告清零：Param 字符串默认 `string.Empty`（`Validate()` 兜底）；Helper 的 `ExcelParam/SQLParam/Name` 给默认；`SqlSugarClient?` 按懒加载语义改为可空 + 本地变量双检锁；EPPlus 捕获 `Dimension` 判空、`CreateItem` 返回 `T?`；测试 `null!`；Workbench 三 ViewModel 字段可空化 + `EnsureX` 局部变量 + `SelectedFile` 空守卫 + `UiThread` 封送。
- `DataManagerModule` 删除无参 `IExcel → MiniExcelHelper` 单例注册（解析后 `ExcelParam` 为空即 NRE，且无人使用），`IExcel` 一律经 `ExcelManager.Create` 获取。
- 结果：`dotnet build DataManager.sln` 0 警告 0 错误；`dotnet test` 99/99。
- 延期（后续测试）：真库 Testcontainers 集成测试。已确认本机无 Docker（`docker` 命令不存在），且 Docker Desktop 需管理员权限 + WSL2 + 重启安装，无法由 Agent 代装。后续步骤：① 手装 [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/)（勾选 WSL2）；② `docker info` 能跑后通知 Agent；③ Agent 加 `Testcontainers.PostgreSql + Testcontainers.MySql` 包，写 `SqlIntegrationTests`（CRUD/方言/分表真库跑），用例默认 Skip、无 Docker 时不影响现有测试。

## 9. EFCore 分表对齐记录

- 新增 `SQL/Definition/DbSplit.cs`：共享 `SplitPeriod` + `DbSplitTable/DbSplitField`（新代码用此；Dapper 旧特性继续兼容）。
- 新增 `SQL/EFCore/Core/EFCoreSplit.cs`：`SplitModelCacheKeyFactory`（表名+DbType 进模型缓存键，否则多表复用首个映射）+ `EFCoreSplitNaming`（命名/路由，与 Dapper 同语义，同时识别新旧两套特性）。
- `EFCoreBase<T>`：`SplitTableName` + 公开 `IsSplitTableEntity/GetSplitTableName/ForkForTable/GetSplitTables`；Add 按实体日期路由、`AddRange` 按表分组、`Query/Update/Delete` 逐表（条件查询内存过滤，与 Dapper 同取舍）、`DeleteAll` 逐表 `ExecuteDelete`、`Connect` 建当期表；`OnModelCreating` 显式 `ToTable`（与此前默认一致，不做 PG 小写化——故 EF 与 Dapper 物理表名大小写可能不同，各管各的表，行为一致）。
- `DapperBase`：`IsSplit/字段/表名` 三处同时识别共享新特性（旧特性行为不变）。
- 测试 `Tests/SQL/EFCoreSplitTableTests.cs` 6 用例（无 DB，读 `Model` 断言映射）：非分表默认表名、日/月/年后缀、DbSplitField、新旧特性识别、fork 映射 + 默认模型不受影响（缓存隔离证明）、同表 fork 共享模型。
- 结果：`dotnet build` 0 警告 0 错误；`dotnet test` 105/105。

## 10. PG 真库集成记录（本机 PG，无需 Docker）

- 用例 `Tests/SQL/PostgresIntegrationTests.cs` 5 个：Dapper/EFCore/SqlSugar CRUD 往返 + Dapper/EF 日分表自动路由；连 `DM_PG_HOST/PORT/USER/PASSWORD/DB`（默认 `127.0.0.1:5432/postgres/<DM_PG_PASSWORD>/dm_integration`），库表自建自清；运行：`$env:DM_PG_PASSWORD="123"; dotnet test --filter PostgresIntegrationTests`。
- 真库揪出 4 个单测发现不了的 bug（已修）：
  1. Dapper 表名：Dommel 默认复数+双引号（`"PgDapperItems"`，SQL 日志实证），手写 DDL 单数小写 → 42P01。修：新增 `DapperNaming` 注册全局小写单数解析器（认 `[Table]/[Column]`），`SqlNamingHelper` MySQL 分支同步小写（单测同步改期望）。
  2. Dapper 主键：Dommel Insert 排除自增主键（`returning`，SQL 日志实证），而手写分表 Insert 显式写 `Id=0`（重跑即 23505），且 Update 盲拷贝把查回的主键覆成 0（WHERE 命中 0 行）。修：`InsertToTable` 跳过默认 `[Key]`（显式赋值仍插入），4 处 Update 改 `CopyUpdatableValues`（跳 `[Key]`）。
  3. EF 建表：`EnsureCreated()` 在“库存在且有表”时直接返回（EF9 源码实证：`else if (!HasTables())` 才建表），跨工具共库时 EF 的表永远建不出 → 42P01。修：`Connect()` 后加 `EnsureTableExists()`（查 `information_schema`，缺则调公开 `IRelationalDatabaseCreator.CreateTables()`，写前同样兜底）。
  4. EF 模型缓存键：自定义 `SplitModelCacheKeyFactory` 丢弃 `designTime` 标记 → 设计时模型请求命中运行时模型 → 建表管线报 read-optimized（二分实证：裸 context 成功，加 `ReplaceService` 即炸）。修：组键补 `designTime`。
- 结果：`dotnet build` 0 警告 0 错误；`dotnet test` 110/110（含真库 5）。
- 注意：诊断探针（`EFCoreDebugProbe`/`DommelSqlProbe`）已删除；`DM_PG_PASSWORD` 未设时集成用例连默认密码会失败（环境问题非代码问题）；Testcontainers 路线不再需要（本机 PG 已覆盖）。

## 11. 工作台密码纠错仍报错记录

- 现象：首次密码填错，改对后重连依然 28P01。
- 根因：`SqlViewModel.EnsureDb` 缓存键只含“工具+库类型”，密码等连接要素改动不触发重建，一直复用首次的旧实例（旧密码）。
- 修：键改为全部连接要素（`BuildDbKey` 公开静态：工具/库类型/IP/端口/用户/密码/库名），任一改动重建并释放旧实例；`Tests` 新增 `Workbench/SqlViewModelKeyTests` 6 用例锁定（为此 Tests 引用 Workbench 工程）。
- 结果：`dotnet build` 0 警告 0 错误；`dotnet test` 116/116。
- 后续：Log/Excel 页同款指纹重建已补（见下）。

## 12. Log/Excel 指纹重建记录

- 同因：Log 页改路径后直接点写入沿用旧实例、Excel 页改文件后直接操作沿用旧实例（SQL 页第 11 节同款 bug）。
- 修：`LogViewModel.BuildLogKey`（类型/名/路径/分隔符/保留天数）+ `ExcelViewModel.BuildExcelKey`（工具/名/路径/文件名/表头）；`CurrentParam/ResetX/EnsureX` 三件套统一，`Ensure` 在键变更时重建并提示“配置已变更，实例已重建”（旧实例释放：Log 调 `Dispose`，Excel 从 Manager 移除）。
- 测试 `Tests/Workbench/LogExcelKeyTests` 9 用例（同键相同、各字段变更换键）。
- 结果：`dotnet build` 0 警告 0 错误；`dotnet test` 125/125。

## 13. 死代码清理记录

- `Log/SeriLog/SerilogHelper.cs`：删除 P0-3 后已无用的 `CustomLevelEnricher`（级别改走 `LogContext.PushProperty`）及从未实现的 `Config()`（调即抛），附带去掉 `Serilog.Core/Events` 无效 using。
- `SQL/SqlSugar/SqlSugarBase.cs`：删除“官方规范分表操作 暂时不使用”整区闲置私有方法（`InitSplitTable/SplitTableAdd(-Range)/SplitTableQuery(-ByTake/-All)/SplitTableUpdate/SplitTableDelete/GetSplitTableName/GetAllSplitTableNames`，grep 确认零引用）。
- 保留：`SerilogHelper.Connect/DisConnect`（有效生命周期 API，虽暂无内部调用）、`SqlSugar` 建表/删表/判存公有 API。
- 结果：`dotnet build` 0 警告 0 错误；`dotnet test` 125/125（纯删除，用例数不变）。

## 14. 工作台刷新刷不出文件记录

- 现象：Log 页点刷新永远 0 个文件，按文件查询/多行读取在工作台不可用。
- 根因：三实现文件名均为 `logyyyyMMdd`（无扩展名，磁盘实证），而 `RefreshFilesCommand` 用 `*.txt` 过滤。
- 修：过滤改为 `log*`；测试 `Tests/Workbench/LogViewModelRefreshTests` 3 用例（三实现各一，端到端走 ViewModel 命令）。
- 结果：`dotnet build` 0 警告 0 错误；`dotnet test` 128/128。

## 15. 工作台端到端冒烟 + Npgsql 开关库层面保证记录

- 测试 `Tests/Workbench/SqlViewModelSmokeTests` 2 用例：无头驱动 `SqlViewModel` 全命令连真库（连接→批量加→查询→条件查→更新→删除→清空，用输出标记轮询判定完成）；密码错→纠正→重连一次通过（第 11 节的端到端覆盖）。
- 冒烟首跑失败定位真 bug：批量写 `DateTime.Now`（Local）被 Npgsql 拒绝，而冒烟文件没设兼容开关（集成测试/探针设了，所以它们能过）。根因是库把开关责任推给宿主（`DataManagerModule`）。
- 修：`EFCoreBase<T>` 与 `DapperBase<T>` 静态构造中自行 `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`（时序早于首次连接，幂等；宿主/测试原有设置保留无害）。
- 结果：`dotnet build`（DataManager 三工程）0 警告 0 错误；`dotnet test` 130/130。

## 16. 工作台 MaterialDesign 改造记录

- 包：`MaterialDesignThemes 5.3.0`（csproj 里已有引用，删掉了笔者误加的 5.1.0 重复项）；Prism 保持 8.1.97，导航/区域/模块零改动。
- `App.xaml`：`BundledTheme`（Light + Indigo + Teal）+ `MaterialDesign3.Defaults.xaml` + 自有 `Theme.xaml`。
- `Theme.xaml` 瘦身：删自绘按钮/输入框/表格/导航样式（Material 接管），仅保留标题字阶、终端风输出框、深色侧栏导航（Chip 徽标 + 硬编码 Indigo 选中）。
- 四视图：`materialDesign:Card` 分区 + `OutlinedTextBox/ComboBox` 浮动提示 + `Raised/Outlined` 图标按钮 + `MaterialDesignDataGrid` + 页头 `PackIcon`；全部绑定/命令原样不动。
- 测试 `Tests/Workbench/WorkbenchXamlSmokeTests` 1 用例：STA 无头完整启动 Prism App，三页逐一导航创建（构建验不了 StaticResource，必须运行时验）。
- 冒烟修过 2 个真问题：① `MaterialDesignTheme.Defaults.xaml` 在 5.3.0 已改名 `MaterialDesign3.Defaults.xaml`（包内 BAML 清单实证）；② 测试脚手架漏调 `InitializeComponent()`（生产入口自动调）。
- 结果：`dotnet build` 0 错误；`dotnet test` 131/131。
- 取舍：未做运行时明暗切换（5.x 主题 API 未经验证，不上未经实证的代码）。

---

## 7. 给 Codex 的执行指令（按序）

1. 先做 P0-1 → 跑 `dotnet test` → 绿灯再往下。
2. P0-2 注意同步改 `SQLManagerTests` 和 `App.xaml.cs` 注册。
3. P0-3 注意读写路径一致性，先读 `LogBase.cs` 再改。
4. P0-4 拆类时保持 `ISQL<T>` 签名不变，Workbench 不用改。
5. 每做完一项更新本文档复选框 + `git commit` 一次。

---

## 8. P3–P7 续记（2026-09-10，`dotnet build` 0 警告 0 错误；`dotnet test` 182/182，需 `DM_PG_PASSWORD`）

- **P3 四库补齐**：`SQLParam` 新增 `FilePath/ConnectionString（直通最高优）/TrustServerCertificate`，`Validate()` 按库分支；
  新 `ISqlDialect/SqlDialectRegistry/SqlDialects`（PG/MySQL/SQLite/SQL Server），`SqlDialectHelper/SqlNamingHelper` 转调（旧 API 不变）；
  Dapper/EFCore(`UseSqlite/UseSqlServer`)/SqlSugar(`DbType.Sqlite/SqlServer`) 全接；`DbConnectionProfile.ToSqlParam` 打通四库；
  新增包 `Microsoft.Data.Sqlite/SqlClient + EFCore.Sqlite/SqlServer`；删 `SqlSugarBase` 误引 `using Dm`。
  SQLite 三工具真库（文件自清，CI 可跑）；SQL Server 无本机库，仅方言生成层覆盖。
- **P3 真 bug**：EFCore 分表 LIKE 被统一小写后 PG 查不到表（EF 表名保留大小写）→ EFCore 恢复原大小写 SQL。
- **P4 接口硬化**：`ISQL` 参数/状态只读 + `IDisposable`；`SqlSugarBase.IsConnected` 去 setter 副作用 + `Dispose`；
  `DeleteAll` 改 `Deleteable` 真异步（TRUNCATE→DELETE，自增种子不再重置）；维护类 Async 去 `Task.Run`；
  `DapperBase` 拆 `DapperSqlBuilder/DapperSplitRouter`（附带修 `AddAsync` 不路由、`DeleteAsync/DeleteAllAsync` 漏分表）；
  `ManagerBase.DataManagers` 私有化；`CancellationToken` 延期（Dommel/SqlSugar 无支撑）。
- **P4 真 bug（旧伤）**：XAML 冒烟起真 Dispatcher 后并行 PG 冒烟 `Log()` 被封送到关闭中的 Dispatcher → `UiThread.Invoke` 加关闭态直行 + 取消回退（全量 40s→13s）。
- **P5 Log 真异步**：查询路径 `ReadAllTextSharedAsync + Task.WhenAll` 去 `Task.Run`，解析抽 `ParseLogInfos` 共用；
  `AddAsync` 改 virtual（sink 全同步，只能转交，留缝）。
- **P6 下推**：EFCore 分表条件查改逐表服务端过滤；Dapper 新 `DapperPredicateTranslator`（比较/与或非/null/bool/枚举，全参数化，不可译回退）；
  白盒 `InternalsVisibleTo(DataManager.Tests)`；实证：编译器把枚举 `==` 降为底层比较（右侧成 Int32 常量），断言按实际锁定。
- **P7 裁剪**：Dapper 分表字段等值约束只查命中表（AND 链提取，多日期交集为空即空集；范围/OR/方法调用全表）；
  约束：勿更新分表字段本身（行不迁移）；工作台分表命令补 EFCore。
- **延期（需外部条件）**：SQL Server 真库（无服务/LocalDB/Docker）、CancellationToken（上游无支撑）、回退路径全表扫描（正确优先）。

## 9. P8 续记（范围裁剪 + EFCore 裁剪 + 工作台 SQLite 端到端，198/198）

- 新 `SplitDatePruner`（Dapper/EFCore 共用）：AND 链提等值/范围约束 → 候选后缀集；等值精确、多日期 AND 空集、
  有界范围按周期枚举（Day/Month/Year，500 槽熔断超限不裁剪）、单边无界/OR/非日期一律 null（全表）。
  边界 inclusive 枚举恒 sound（含边界槽，严格不等同样覆盖），等值+范围组合超界即空集。
- Dapper 路由器改走共享器（删重复实现）；EFCore `ScopedForQuery(predicate?)` 同等裁剪（fork 精确大小写求交，
  无参查询/清空保持全表）；谓词方法（查/改/删×同步异步）自动生效。
- 工作台 SQLite 无头端到端 1 用例（`SqlViewModel` 全命令走真文件，CI 可跑）；Log 微清理（`ParseLogInfos` 分隔符 hoist、删死变量）。
- 计数：198 = Excel 20 / Log 32 / Manager 12 / SQL 96 / Workbench 38。

## 10. P9 续记（API 对齐，201/201）

- `SqlSugarBase.GetSplitTables()`（DbMaintenance 前缀匹配，模板 `{` 前部分，双下划线坑已避），工作台分表命令三路全支持；
  SQLite 实证揪出 2 个原生约束：long→BIGINT 与 SQLite AUTOINCREMENT 冲突（实体 `ColumnDataType="integer"`）、
  分表禁自增主键（跨表撞号，改显式赋值）——已记 README。
- SQL Server 向导补 `TrustServerCertificate` 字段（Compose 早自动补，界面不可见不可改）。
- Month 周期 SQLite 端到端（路由 + 月裁剪）。
- `ISQL<T>.Update(T)/UpdateAsync(T)` 入接口三路对齐（Dapper 早有未入接口；EFCore 整行语义 + 分表路由；
  长驻 DbContext 跟踪冲突内部消化：已跟踪同键 `SetValues`，支持复合主键元数据判定）。
- 计数：201 = Excel 20 / Log 32 / Manager 12 / SQL 99 / Workbench 38。

## 11. P10 续记（单实例事务，205/205）

- `ISQL<T>.ExecuteInTransaction(+Async)`：同连接原子提交，异常回滚；嵌套抛。
  Dapper 环境事务（作用域释放器不断开共享连接；全部命令显式 enlist；Dommel 按连接运行时类型选方言，
  故不用包装类——包装会破坏识别，作用域释放器一行替换无重排进）。
- EFCore 显式连接事务（分表抛 NotSupported，fork 独立连接，静默非原子更危险）；
  SqlSugar 显式 Begin/Commit/Rollback（不用 `Ado.UseTran`：它吞业务异常只回滚不抛，实证）。
- SQLite 锁定：三路提交/回滚、嵌套抛、Dapper 跨表原子、EFCore 分表抛；
  附带约束：SqlSugar 在 SQLite 下无自增（IsIdentity 不支持，显式 Id；连带解释此前 0 行之谜——Id=0 主键冲突）。
- 计数：205 = Excel 20 / Log 32 / Manager 12 / SQL 103 / Workbench 38。

## 12. P12 续记（新功能 PG 验证，210/210，两次连续全绿确认幂等）

- 新 `Tests/SQL/PostgresFeatureTests` 5 用例：Dapper 下推+裁剪、事务回滚、单更新、EF 裁剪（大小写精确）、SqlSugar 分表列表。
- 真 bug（旧伤）：集成测试 `DropTablesLike` 连 `postgres` 系统库，清表空转、跨 run 残留（旧测试靠 DeleteAll+自增扛住）。
  修：新文件清真实测试库 + 测试先清 + Sugar 显式 Id 用 run 内唯一值。
- 计数：210 = Excel 20 / Log 32 / Manager 12 / SQL 108 / Workbench 38。

## 13. P13 续记（分析健全性锁，215/215）

- MySQL 探活：3306 关、无服务无目录——同 SQL Server 被环境卡，方言层覆盖止步。
- 裁剪新增锁：翻转边界、OR 嵌套（范围仍生效，不下钻）、null 等值不裁剪；
  立规矩时亲手验算错两处（inclusive 上界含边界表、OR 顶层不裁剪），以实现为准，已修正断言。
- EF Month 端到端 + SqlSugar 非分表空列表契约。
- 计数：215 = Excel 20 / Log 32 / Manager 12 / SQL 113 / Workbench 38。

## 14. P14 续记（无密码基线实证）

- 不设 `DM_PG_PASSWORD` 全量：恰 12 失败（集成 5 + 新功能 5 + 冒烟 2），其余 203 全绿；
  README 原写 17 系笔误，已校准为 12。

## 15. P15 续记（Helper 归位 + 并行重构修复，218/218）

- `SqlDialectHelper/SqlNamingHelper`：`Dapper/Helpers` → `SQL/Definition`（P3 起即共享层，原位置名不副实），空目录删除，Dapper/Workbench/Tests 引用跟随。
- 并行重构（`SqlWorkspace` 抽取）修复：新 VM 补 using；`BuildDbKey` 被抽回 7 字段→恢复 10 字段（否则 SQLite 换文件不重建）；
  字符集写坏的字面量、注释合并的死语句、最后一个旧构造、新字段指纹用例，逐一修复。
- 计数：218 = Excel 20 / Log 32 / Manager 12 / SQL 113 / Workbench 41。

## 12. P11 续记（覆盖补齐 + 卫生，205/205）

- 事务同步/异步三路全覆盖（既有单测只覆盖半侧）；helper 杂散 using 清理。
- 仍未动：SQL Server 真库、CancellationToken（上游）、回退全扫、EPPlus 废弃 API（兼容保留）。

## 16. P16 续记（LogManager 原子配置替换）

- `ManagerBase` 新增单一管理器互斥边界，`Items`、索引器、增删、查询和批量替换共享同一锁，读取方只能观察替换前或替换后的完整快照。
- `LogManager.ReplaceConfigured()` 先构造全部新实例，再按调用方拥有的名称集合验证外部冲突并原子提交；构造/校验失败释放候选实例且保留旧集合，提交成功后才释放旧日志。
- `LogConfigService.Load()` 改用该深模块 interface，删除宿主侧“先移除、再逐项注册”的重复事务逻辑。
- `LogManagerTests` 专项 11/11 通过（含并发读取只能观察完整替换前/后快照）；DataManager 全套本机结果 169/179，剩余 10 项均为既有 PostgreSQL 密码认证失败。

## 17. P9 续记（仓库整体优化 P9：异步取消 + 命令超时 + 退出安全）

> ⚠️ **与 §10「P9 续记（API 对齐）」不是同一件事**：§10 那条是 DataManager **内部**的历史分期
> （P8/P9/P10 指 DataManager 自己的优化轮次）；本条是仓库**整体**优化计划的第 9 期
> （`Software-Framework_P9修改计划_2026-09-28.pdf`，基线 `f39b6a74` = P8 提交）。两者编号撞车，勿混。

- **纠正上一版错误认识**：`Prism` 与 `Extension` 都是**基础必需库**，不做隔离层、不做移除审计，也不再追求把
  DataManager 改成纯 `net9.0`。理由：`DataManagerModule : IModule` 本身就是 Prism 的模块契约；Extension 提供主题
  （`ThemeService`）、配置/JSON 读写、`DispatcherEx` 等共享基础设施，且自身是 `net9.0-windows7.0 + WPF`，
  TFM 改不掉。两条依赖由守卫测试 `SqlDependencyArchitectureTests` 锁定。
- **P9-2 `ISQL<T>` 可取消重载**：9 个异步入口各加一个带 `CancellationToken` 的重载
  （`AddAsync` / `AddRangeAsync` / `QueryAsync()` / `QueryAsync(predicate)` / `DeleteAsync(predicate)` /
  `DeleteAllAsync` / `UpdateAsync(T)` / `UpdateAsync(predicate,T)` / `ExecuteInTransactionAsync`）。
  **旧签名全部保留**并内部转调 `CancellationToken.None` ⇒ 源码兼容，既有调用点零改动。
  Token 只走参数，**不进 `SQLParam`**（SQLParam 是连接配置，不含单次调用的运行时状态）。
- **P9-3 三 provider 取消链路对齐**：
  - EFCore：原生可取消 API（`ToListAsync` / `SaveChangesAsync` / `ExecuteDeleteAsync` / `AddAsync` /
    `BeginTransactionAsync` / `CommitAsync`）；分表路径逐表 `ThrowIfCancellationRequested()`。
    `SaveChangesOptimizedAsync` 新增 `catch (OperationCanceledException) { …; throw; }` 并放在通用 catch **之前**
    ——否则 OCE 会被包装成普通 `Exception`，上层无法区分「取消」与「业务失败」（闸门第 3 条）。
  - Dapper：非分表走 Dommel 的 `cancellationToken:` 参数；分表统一走新增的 `DapperCommandFactory.Create(...)`，
    把 `commandTimeout` 与 `cancellationToken` 一起映射进 `CommandDefinition`。
  - SqlSugar：**非分表**原生 `ExecuteCommandAsync(token)` / `ToListAsync(token)`；**分表** provider
    （`SplitInsertable` / `SplitTableUpdateByObjectProvider` / `SplitTableDeleteByObjectProvider`）
    **没有 Token 重载** ⇒ 只做「进入前检查」并显式注释边界，**不假装可中断**（拒绝用 `Task.Run` 包装伪造取消）。
  - 事务：三路都是「进入前 + 提交前」各检查一次；catch 里回滚一律用 `CancellationToken.None`
    ——回滚是取消的补救动作，绝不能被取消跳过。
- **P9-4 统一命令超时**：`SQLParam.CommandTimeoutSeconds`（秒，**默认 0 = provider 默认**，现场行为不变；负值 `Validate()` 拒绝）。
  映射：Dapper → `CommandDefinition.commandTimeout`（0 → null）；EFCore → `UseXxx(conn, o => o.CommandTimeout(...))`；
  SqlSugar → `IAdo.CommandTimeOut`。EFCore 超时在构造 `DbContext` 时固化 ⇒ `BuildDbKey` 纳入 `CommandTimeoutSeconds`，
  改值触发实例重建。**不重写连接串**（直通串优先级契约不变）。
- **P9-5 工作台可取消 + 退出安全**：`SqlWorkspace<T>` 变 `IDisposable` 并持单一在途 `CancellationTokenSource`：
  `BeginOperation()` 签发新 Token 前先取消旧的（同一 Workspace 不并发跑两套数据库任务）；
  重连 / 切换档案 / 断开 / 释放都先 `CancelInFlight()`；`Dispose()` = 取消 → `DisConnect` → 从 `SQLManager` 摘除 → `Dispose`，
  之后 `EnsureDb` 抛 `ObjectDisposedException`。`PostToModel` 双检（已释放 / 已取消）保证关闭后不再回写界面。
  `SqlViewModel` 新增 `CancelInFlight()`，`RunWork/RunAsync/Run` 增加 `catch (OperationCanceledException)` 分支，
  按「… 已取消」记录而不是 `✗` 错误。`Workbench/App.xaml.cs` 的 `OnExit` 释放 `SqlWorkspace<TestEntity>`。
- **测试锁定的 provider 边界（重要：勿当成「已实现的取消」）**：
  1. `Microsoft.Data.Sqlite` 在**库级锁等待**中不响应取消，按连接串 `Default Timeout` 超时后抛
     `SqliteException`（SQLITE_BUSY），**不是** `OperationCanceledException`（独立探针 + 测试双证）。
  2. SqlSugar 分表 provider 无 Token 重载（见上）。
  3. 写 SQLite 锁用例的环境细节：必须 `Pooling=False`（默认连接池会一直持有文件句柄 ⇒ blocker 拿不到独占锁）
     \+ `PRAGMA journal_mode=DELETE`（WAL 模式下读不被写阻塞）。
- 新增测试文件：
  - `DataManager.Tests/SQL/SqlAsyncApiCompatTests.cs` —— 旧（无 Token）签名冻结，三 provider 语义不变。
  - `DataManager.Tests/SQL/SqlDependencyArchitectureTests.cs` —— Prism/Extension 基础依赖 + AnyCPU（PE 头）+ 不反引宿主工程。
  - `DataManager.Tests/SQL/SqlCancellationTests.cs` —— 预取消无副作用且不污染实例、事务取消回滚、取消不被吞成空集合。
  - `DataManager.Tests/SQL/SqlCommandTimeoutTests.cs` —— 超时映射到三 provider（含反射读 SqlSugar 私有 client）。
  - `DataManager.Workbench.Tests/SqlViewModelCancellationTests.cs` —— 工作台取消与退出安全（含 SQLite 锁边界）。
- 闸门遵守：①先补测试再改实现 ②新 API 保持旧 API 源码兼容 ③取消一律向上抛 OCE、不吞成空集合/false
  ④事务中取消与异常同样回滚 ⑤`CommandTimeoutSeconds` 默认 0 不改变现场行为 ⑥全部 Any CPU，未新增 x64。
- 计数（实测）：DataManager.Tests **310**（288 通过 / 22 失败）+ DataManager.Workbench.Tests **67**（65 通过 / 2 失败）= **377**，
  24 条失败**全部**是外部真库实例缺失（PG 10 + MySQL 6 + SQL Server 6 + PG 冒烟 2；本机 5432 连接超时、3306 连接拒绝），
  **非回归**；P9 新增 39 条**全绿**。
  `dotnet build DataManager/DataManager.sln --no-restore -m` = **0 错误**；P9 改动文件**零新警告**。
