# DataManager 优化计划（待改项存档，Codex 可直接执行）

> 生成日期：2026-09-07
> 仓库子目录：`DataManager/`
> 现状基线：`dotnet build DataManager.sln` 0 错误（DataManager 三工程 0 警告；Extension 旧警告与本库无关）；`dotnet test` 130/130 通过（需 `DM_PG_PASSWORD` 环境变量）。
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
- 用例分布（130 个）：Excel 20（EPPlus 8 + MiniExcel 6 + Manager 6）/ Log 28（Serilog 8 + NLog 6 + log4net 6 + Manager 8）/ Manager 12 / SQL 50（EFCore 分表 6 + PG 真库集成 5 + 工厂/命名/方言等 39）/ Workbench 20（SQL 指纹 6 + Log/Excel 指纹 9 + 刷新 3 + 冒烟 2），TDD 证据同下。
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

---

## 7. 给 Codex 的执行指令（按序）

1. 先做 P0-1 → 跑 `dotnet test` → 绿灯再往下。
2. P0-2 注意同步改 `SQLManagerTests` 和 `App.xaml.cs` 注册。
3. P0-3 注意读写路径一致性，先读 `LogBase.cs` 再改。
4. P0-4 拆类时保持 `ISQL<T>` 签名不变，Workbench 不用改。
5. 每做完一项更新本文档复选框 + `git commit` 一次。
