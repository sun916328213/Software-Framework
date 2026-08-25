# Secs 项目优化实施计划

- 编制日期：2026-08-24
- 参照蓝图：`C:\Users\91632\Desktop\V1.0\SecsGem`（设备侧协议框架）
- 改造对象：`C:\Users\91632\Desktop\repos\控件\C#框架\Secs\Secs`
- 前置文档：`C:\Users\91632\Desktop\SecsGem_vs_Secs_对比分析报告.md`（2026-08-19，结论已复核仍然成立）

---

## 一、总体思路

以 SecsGem 的架构思想为蓝本来改造 Secs，但**不推翻重来**：

| 保留（Secs 的业务价值） | 替换（借鉴 SecsGem） |
|---|---|
| Order 类模型（S1F1.cs ~ S9F9.cs，业务好扩展） | 回复匹配机制 → 接收循环直接 TryReplyAsync |
| VID/CEID/ReportID/Alarm CSV 配置体系 | 分散状态逻辑 → 统一 EQStateMachine 式状态机 |
| WPF UI / DataManager 日志集成 | BeginInvoke 事件分发 → Channel/顺序 await |
| net9.0 + Prism 宿主 | 反射扫描工厂 → 缓存注册表 |
| | catch 返回 null 触发 S9F7 → 统一 S/F 描述表 + S9Fx 兜底 |

**改造顺序原则**：先修正确性（会崩溃/丢状态的），再改架构（难维护的），最后补功能（缺失的）。每阶段结束必须可编译、可通过该阶段新增的回归测试。

---

## 二、本次复核新发现的问题（补充进计划范围）

以下问题在原报告中未列出或描述不足，本次读码确认：

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| N1 | **断线后 Mode 状态永远停留在 Online_Remote**：`SecsControl_OnConnectChanged` 在非 Selected 时调 `UpdateSEMIMode(Offline)`，但 `UpdateSEMIMode` 开头 `if (!SecsControl.IsConnected) return;` —— 断线时 `IsConnected` 已为 false，直接早退，Offline 写不进去 | `StatusManager.cs:99`、`SecsControl.cs:70-76` | 状态机失真，重连后 Host 拿到错误模式 |
| N2 | **未知消息直接抛异常且无人接收**：handler==null 时 throw，而事件是 `BeginInvoke` 触发的，异常被吞；Host 侧只会等到 T3 超时，而不是收到 S9F3/S9F5 | `SecsControl.cs:122-126` | 协议行为错误 + 故障不可见 |
| N3 | **`DataBase.Save()` 是 fire-and-forget**：`CSVEx.ExportToCsvAsync(...).ConfigureAwait(false)` 未 await，保存是否成功完全不可知，进程退出可能丢配置 | `DataBase.cs:48` | 配置丢失风险 |
| N4 | **`Send(SecsMessage, IsWait)` 忽略 IsWait**：构造了 `replyMessage` 却发送原始 `pSecsMessage` | `SecsHelper.cs:383-398` | 发送语义不一致 |
| N5 | **`throw ex` 重置调用栈**（多处）：`SecsBase_OnReceiveAsync`、`SendItem`、`Send` 等 | `SecsControl.cs:135`、`SecsHelper.cs:373` | 异常定位困难 |
| N6 | S6F11 `GetSendItem` 失败返回 null 后仍继续发送（SecsItem=null 的空消息），而不是走异常路径 | `S6F11.cs:78-82` | 可能发出畸形消息 |
| N7 | 收到的 `SecsMessage`/`PrimaryMessageWrapper` 从不 Dispose。官方 secs4net 明确要求：Item/SecsMessage 实现 IDisposable，内部用 MemoryOwner 池化内存，不 Dispose 则数组要等 GC 才归还池 | `SecsHelper.cs` 接收路径 | 长时间运行内存压力增大（高频消息场景） |
| N8 | HSMS 定时器只有 T3 在用；E37 规定的 T5/T6/T7/T8 未暴露配置，也未按设备常量管理 | `SecsGemOptions` 构造处 | 断线检测/T7 未选中超时等行为不受控 |

---

## 三、分阶段计划

### Phase 0：护栏与基线（约 3 天）

> 目的：后续任何重构都有"没改坏"的证据。当前项目零测试。

| 任务 | 内容 | 产出 |
|------|------|------|
| 0.1 新建测试工程 | `Secs.Tests`（xUnit，net9.0），引用 Secs 项目；把 `secs4net-base/test` 中可复用的管道连接测试思路搬过来 | 测试工程可运行 |
| 0.2 内存传输抽象（提前做，Phase 1 依赖它） | 定义 `ISecsTransport`（Send/State/PrimaryMessages 流），先包一层现有 `HsmsConnection + SecsGem`；同时实现 `InMemorySecsTransport`（参照 SecsGem `InMemorySecsConnection`），支持测试中注入模拟 Host 消息 | 两个实现 + 冒烟测试 |
| 0.3 关键路径特征化测试 | 对现状写"行为快照"测试：① 收到 S1F13/S2F31/S10F3 能回正确 F+1；② ReplyAsync 找不到主消息抛 TimeoutException；③ 工厂能解析全部现存 Order 名单 | 特征化测试集（允许暴露现有缺陷，标记 `[Fact(Skip)]` 待修复后启用） |
| 0.4 外部 Host 模拟器选型（联网调研结论） | 无真机时的联调手段按优先级：① 自建 InMemory 传输 + 单测（最快、可进 CI）；② 开源桌面仿真器 **secs-simulator**（electron，支持 SML 加载/自动回复/AutoFlow 场景链）或 **oca_gem_tool**（浏览器版，内置 E30 五层状态机可视化和 JSON 场景跑批）；③ Python **secsgem** 写宿主侧脚本化集成测试 | 选型记录 + 至少接通一种 |

---

### Phase 1：P0 正确性修复（约 1~2 周）

> 目标：消除崩溃、吞异常、丢状态、超时匹配这四类现场故障。

#### 1.1 重做回复匹配（最高优先级）

- **现状**：`GetPrimaryMessageAsync` 把 `PrimaryMessageWrapper` 堆进 `_primaryMessages` 列表；`ReplyAsync` 用信号量 + `while + Task.Delay(10)` 轮询 + `FirstOrDefault` 按 S/F 线性查找，最长等 T3=45s。并发两条同 S/F 主消息时会拿错对象。
- **方案**（参照 SecsGem `EQSecsRuntime.RunAsync`）：
  1. 删除 `_primaryMessages` 缓存列表与轮询。
  2. 接收循环内直接构造处理上下文并分发：
     ```csharp
     await foreach (var primary in mConnection.GetPrimaryMessagesAsync(token))
     {
         var ctx = new ReceiveContext(primary.PrimaryMessage, sml, primary); // 携带 wrapper
         try { await HandlePrimaryAsync(ctx); }          // 原 OnReceive 逻辑
         catch (Exception ex) { RaiseError(...); /* 单条隔离，不断循环 */ }
     }
     ```
  3. `ReplyAsync(ReceiveContext ctx, Item item)` 直接 `await ctx.Wrapper.TryReplyAsync(reply)`——O(1)、无竞态、无轮询。
  4. `SecsHandle` 委托签名升级为携带上下文（保留旧签名一个版本，标 `[Obsolete]`）。
- **改动文件**：`SecsHelper.cs`（核心）、`SecsControl.cs`
- **验收**：并发注入 50 条混合主消息，全部在 <100ms 内得到正确回包；无 TimeoutException 误报。

#### 1.2 移除 BeginInvoke

- **现状**：`MessageIn` 里 `OnReceive?.BeginInvoke(...)`，异常全吞、Handler 移除后仍触发。
- **方案**：接收循环本身串行，改为直接 `await` 分发；日志（log.Add）留在循环内同步执行。`OnSend/OnReply` 保留同步 `?.Invoke` 并加 try/catch + RaiseError。
- **改动文件**：`SecsHelper.cs`
- **验收**：处理器抛出的异常能在 `OnError` 事件中观测到。

#### 1.3 修 async void 与同步阻塞

- `VIDManager.UpdateAlarm` → `async Task`，加 try/catch 上报；调用方（UI/业务）同步适配。
- `Connect()` 同步包装标 `[Obsolete]`，内部改 `Task.Run` 保留但打警告日志；新代码一律 `ConnectAsync`。
- `Close()` 顺序修正：先 `Cancel()` → 等待接收循环退出 → `DisposeAsync()` connector → Dispose secsGem → Dispose CTS；删除 `CloseAsync` 里的多余 `Task.Run`。
- `Dispose(bool)` 中去掉 `Task.Run(() => CloseAsync()).GetAwaiter().GetResult()`，改调同步 `Close()`。

#### 1.4 修复状态丢失 Bug（本报告 N1）

- `UpdateSEMIMode/UpdateDeviceStatus` 的 `if (!IsConnected) return;` 门禁移除或改为参数 `bool requireConnected`；断线路径必须能把 Mode 写为 Offline。
- 短期最小修复：`SecsControl_OnConnectChanged` 里直接设 `CurrentMode`（绕过门禁），Phase 2 由统一状态机彻底替换。

#### 1.5 消息生命周期与定时器（本报告 N7/N8）

- 接收循环改造（1.1）时顺带补齐 `using`/`Dispose`：`PrimaryMessageWrapper`、收到的 `SecsMessage`、大 Item 用完即还池；日志如需保留 SML 文本，先 `ToSml()` 再 Dispose。
- 把 T3/T5/T6/T7/T8 收敛进统一配置对象（`IOptions<SecsOptions>`），默认值对齐 E37 建议（T3=45s、T5=10s、T6=5s、T7=10s、T8=5s），Phase 3 再评估是否作为设备常量（S2F29/S2F30）暴露给 Host。

#### 1.6 死代码与明显缺陷清理

| 项 | 文件 | 动作 |
|---|---|---|
| `ManagerBase.Dispose` 抛 NotImplementedException | `ManagerBase.cs:25` | 实现为标准 Dispose 模式（虚方法 + disposed 标记） |
| 未知消息 throw 吞掉（N2） | `SecsControl.cs:122` | Phase 1 先改为回 S9F7 兜底 + RaiseError；Phase 2 描述表接管 |
| `throw ex` → `throw;` 或包装（N5） | 多处 | 全局替换 |
| `OnWaring` → `OnWarning` | `SecsHelper.cs:83` | 加旧名 `[Obsolete]` 别名一个版本 |
| `DeviceManager.Load` 缺键保护 | `DeviceManager.cs:49` | `TryGetValue` + 默认值 + Warning 日志 |
| `AppendVID` 自述"有问题" | `VIDManager.cs:101` | 用 `is List<object>` 精确匹配替代 `IList` 强转；补单测 |
| 未使用的 `_semaphore`(SecsHelper)、`obj_send/obj_reply`、空 `OnReply` 处理器 | `SecsHelper.cs` | 删除 |
| `DataBase.Save` 不等待（N3） | `DataBase.cs:48` | 改为同步 `Save()` 内联写入 + 临时文件原子替换（File.Replace） |

**Phase 1 出口标准**：特征化测试全绿；手工冒烟（SecsDemo 连模拟 Host）通过建立通信/上线/事件上报/报警四场景。

---

### Phase 2：P1 架构重构（约 2~4 周）

> 目标：协议行为单一事实来源 + 可测试 + 解耦。

#### 2.1 统一状态机（参照 `EQStateMachine`）

- 新建 `Secs/State/SecsStateMachine.cs`：
  - `CommunicationState { NotCommunicating, Communicating }`
  - `ControlState { Offline, OnlineLocal, OnlineRemote }`
  - 快照 + `StateChanged` 事件 + `TryGoOnlineRemote()` 等守卫方法 + `EnsureCanSendNotification()` 门禁。
  - （借鉴 SecsGem.NetCore 的 `StateChange` 事件设计）迁移事件携带可否决的 `Accept` 标志：业务方在事件回调里置 `Accept=false` 可取消本次状态迁移（如 Host 请求上线但设备条件不满足时拒绝 S1F17），`Force` 时降级为纯通知——这比"先改后报"的模型更符合现场需求。
- 接线点：
  - `HsmsConnection.ConnectionChanged` → `MarkCommunicationEstablished/Lost`
  - S1F13/14 → Communicating；S1F15/17/S2F41 上线下线命令 → ControlState 迁移
  - `StatusManager` 退化为状态机的"投影 + CEID 上报触发器"，删除自身 Mode/Status 字段与锁
  - `ConnectManager.WaitOnlineAndOfflineAsync` 三任务竞速循环删除，由状态机事件驱动
- **验收**：任意顺序的连/断/上线/下线操作序列，最终状态与 SEMI E30 表格一致（写成参数化测试用例表）；CEID 映射覆盖每个控制状态迁移（合规审计高频失分点，见 6.3）。

#### 2.2 S/F 描述表 + 统一 S9Fx 兜底（参照 `EQProtocolEngine.CreateDescriptors`）

- 新建 `Order/SecsOrderDescriptor`：`(byte S, byte F) → { OrderType, 默认回包工厂, 异常回包工厂 }`，启动时从工厂注册表一次性生成。
- 接收分发改为查表：
  - 无描述项 → S9F3（未知 Stream）/S9F5（未知 Function）
  - 解析失败/返回 null → S9F7（Illegal Data），并 RaiseError（替换现在各 Order 里 `catch return null` 注释约定）
  - 发送侧 T3 超时 → S9F9 自动补发（把 `SendItem` 里手写的 S9F9 下沉到这里）
- 各 Order 类只保留 `GetReplyItem/GetSendItem` 业务逻辑，删除各自的 catch-null 约定。
- **验收**：对每个已支持 S/F 发畸形 Item，Host 都收到结构正确的 S9Fx 而非超时。

#### 2.3 工厂缓存化（消除热路径反射）

- **现状**：`GetHandler` 每次调用 `Assembly.GetTypes()` + LINQ 全扫（每条收发消息都执行）。
- **方案**：启动时扫描一次，构建 `Dictionary<string, Type>` + 二级 `Dictionary<string, SecsOrderBase>` 实例池（单例语义不变）；注册规则从"类名 S 开头含 F"改为显式特性：
  ```csharp
  [SecsOrder("S6F11")] class S6F11 : SecsOrderBase { ... }
  ```
  扫描时优先读特性，无特性再按命名规则兜底并打警告。
- **验收**：`GetHandler` 从 O(程序集类型数) 降为 O(1)；重复调用零反射分配（用 BenchmarkDotNet 或简单计数器验证）。

#### 2.4 数据绑定抽象（参照 `EQDataBindingService` 的 Getter 注册思想，轻量化）

- 引入 `IEquipmentDataProvider`：`object GetValue(string vidName)`，默认实现读现有 `VIDInfo.Value`（内存字典）。
- `VIDManager.UpdateVID` 之外增加 RegisterGetter 通道：PLC/DB 场景由宿主注入 getter，读取 SVID 时实时拉取，不再要求业务方手动刷内存值。
- **不做**：不引入 SecsGem 的整套 AST/Codec（net9 + 直接 Item 操作已够用，避免过度设计）。

#### 2.5 解耦服务定位器（渐进式）

- `ManagerBase`/`SecsOrderBase` 中 `ContainerProvider.Resolve<SecsControl>()` 属性链是循环依赖源（SecsControl↔Managers）。
- 第一步（本期）：提取 `ISecsController` 接口（SendItem/ReplyAsync/StateMachine/VIDManager 访问），Manager 通过接口持有，Prism 注册不变 —— 消除对具体类的依赖，测试可用假实现。（接口方法清单可参考 SecsGem.NetCore 的单一 `ISecsGemServerEventHandler`：Init/GetXxx/SetXxx/CommandExecute/TerminalDisplay/StateChange/OrphanMessage/Error）
- 第二步（远期）：构造函数注入替代 Resolve，脱离 Prism 也能 new（为 Phase 4 测试铺路）。

---

### Phase 3：P2 功能补齐（约 2 周）

| 任务 | 内容 | 参考 |
|---|---|---|
| 3.1 远程命令回调化 | S2F41 目前把命令塞进 `ActiveReceive` 列表让业务轮询；改为 `RegisterRemoteCommand(string name, Func<args, byte ack>)` 注册回调，收到即解析并自动组 S2F42（含 HCACK 码） | SecsGem `RegisterRemoteCommand` + `RemoteCommandParser` |
| 3.2 补全 Trace | 实现 S2F23/24 解析（DRACK）、按 DSPER/TOTSMP 后台采样、S6F1 上报；若短期不做，删除 `TraceManager.GetData` 注释尸体并在 README 声明不支持 | `TraceRequestParser` |
| 3.3 CSV 原子化 + 校验 | Load 时字段校验（ID 唯一性、格式合法），Save 走 temp+replace（Phase 1 已做基础版）；路径从 `"Config//SecsJson//VID"` 收敛到统一配置服务（IOptions） | `ComposerDefinitionStore`/`CsvFileReader` |
| 3.4 底层库评估 | 评估本地 fork `secs4net-base` vs 官方 NuGet 2.4.4+：diff fork 改动点；若仅小修补，提 PR 后切官方包 + 扩展类 partial 补齐；Json/Sml 用官方同名包 | 对比 V1.0 已用官方包的事实；Siemens 公开评估认为 secs4net 是最完整开源实现，切换风险低 |
| 3.5 主动上报增强 | S6F11/S5F1 发送封装超时（T3 可配）+ 重试次数 + Offline 门禁（状态机 `EnsureCanSend`） | `SendPrimaryWithRetryAsync` |

---

### Phase 4：P3 工程化（持续进行）

1. **测试体系扩展**：基于 `InMemorySecsTransport` 建协议级集成测试（模拟 Host 脚本：建链→在线→远程命令→报警→事件→离线全程）；目标覆盖所有已注册 S/F。CI 用 InMemory + Python secsgem 脚本；人工验收用 oca_gem_tool / secs-simulator 走场景表。
2. **README + 合规矩阵**：采用 SEMI E30 行业通用的 22 项能力合规矩阵模板（见 6.3），逐项标注 Supported/Partial/Not Supported，附配置样例（参照 `ConfigSample/README.md`）。
3. **CI**：build + test + 未来 lint（`.editorconfig` 已有基础则启用 warnings-as-errors 白名单）。
4. **代码清理收尾**：`MessageHeader_Refactor.cs` 合并或删除、`MessagePrompt` UI 与核心解耦（事件出、UI 进）、命名空间 `SemiTool.Tool` 归位到 `Secs.Tool`。

---

## 四、里程碑与工作量总览

| 里程碑 | 内容 | 工作量 | 出口标准 |
|---|---|---|---|
| M0 | 测试工程 + 内存传输 + 特征化测试 + 模拟器接通 | ~3 天 | dotnet test 全绿 |
| M1 | P0 正确性修复（含 N7/N8） | 1~2 周 | 并发回包测试 + 冒烟四场景通过 |
| M2 | 状态机 / 描述表 / 工厂缓存 / 数据抽象 / 接口化解耦 | 2~4 周 | E30 状态表测试全绿；S9Fx 全兜底；热路径零反射 |
| M3 | 远程命令回调化 / Trace / CSV / fork 评估 / 上报重试 | ~2 周 | 功能矩阵更新 + 对应单测 |
| M4 | 集成测试 / 文档合规矩阵 / CI / 清理 | 持续 | CI 绿 + README 发布 |

依赖关系：M0 → M1 →（M2 可与 M3 并行拆人）→ M4。

## 五、风险与对策

| 风险 | 对策 |
|---|---|
| 回复匹配重写改变对外事件语义，业务代码（Packing Machine 等）受影响 | 保留旧委托签名一个版本 + `[Obsolete]`；M1 只换内部实现不动公开 API |
| 状态机替换期间双状态源打架 | M2.1 完成前禁止新增任何读写 `CurrentMode/CurrentStatus` 的代码；切换一次 PR 完成，不留过渡态 |
| 本地 fork 切换官方包引入行为差异（如 T3 处理） | M3.4 仅评估出 diff 清单，切换独立成 PR 并跑全量集成测试 |
| 无 Host 真机环境 | InMemory 传输先行 + 外部模拟器（oca_gem_tool/secs-simulator/secsgem 三选一），真机冒烟安排在现场联调窗口执行固定脚本 |

---

## 六、联网调研补充（2026-08，外部方案借鉴）

对 GitHub 及行业资料做了新一轮检索，确认本计划方向与业界主流一致，并补充以下可借鉴点：

### 6.1 C# 开源方案横向结论

| 方案 | 定位 | 对本计划的启示 |
|---|---|---|
| **mkjeff/secs4net**（官方，MIT，~640★） | 底层 SECS-II/HSMS 库；`GetPrimaryMessageAsync + TryReplyAsync` 是官方唯一推荐的应答模式；DI 集成 `AddSecs4Net<TLogger>(Configuration)`，配置走 appsettings.json | ① 印证 Phase 1.1"接收循环直接回复"是正统做法，Secs 现在的轮询队列属于反模式；② Item/SecsMessage 必须 Dispose（池化内存）→ 已列入 N7；③ 配置管理应对齐官方 IOptions 模式 → 支撑 M2.5/M3.3 |
| **harrryhsu/SecsGem.NetCore**（MIT） | 设备侧内核：Feature 集合（SV/DV/EC/Alarm/CE/Command/Terminal）+ 单一 `ISecsGemServerEventHandler` 事件接口 + 内置 `SecsGemClient` 测试客户端 | ① "单一事件处理器接口"比 Secs 现在的多个 Manager 更易实现与 mock——Phase 2.5 的 `ISecsController` 可参考其方法清单（Init/GetXxx/SetXxx/CommandExecute/TerminalDisplay/StateChange/OrphanMessage/Error）；② 其 `StateChange` 事件支持否决迁移（Accept=false），已吸收进 Phase 2.1；③ 内置 Client 的思路强化了 M0.4 选型 |
| **fateit/easysecsgem**（薄封装） | `IHsmsMessageProcess.receiveMessage(PrimaryMessageWrapper)` 回调里直接 `TryReplyAsync` | 同样印证直回模式；其三接口划分（状态/消息/日志）可作为 `ISecsTransport` 接口切面的参照 |
| Siemens 2025 内部评估（公开 slides） | 认为 secs4net 是"已知最完整的开源实现"，但 .NET 与其边缘栈不合而选 Python secsgem | 印证底层库选择无需更换，fork→官方包迁移（M3.4）风险低 |

> 结论：不引入第三方设备侧框架替换自研 Secs 层。上述方案要么太薄（easysecsgem）、要么绑定 AspNetCore 宿主模型（SecsGem.NetCore）、要么只做传输层（secs4net）；Secs 现有的 Order 类 + VID CSV 业务模型仍是差异价值，改造重点保持在计划既定的 P0/P1 项上。

### 6.2 测试工具生态（支撑 M0/M4）

- **oca_gem_tool**：浏览器版仿真/测试工具，亮点是 E30 五层状态机可视化（HSMS→Communication→Control→Process→Spooling 级联）+ State Sync Matrix + JSON 场景跑批（内置正常流/异常流用例）。适合作为**状态机验收的可视化对照物**和现场演示工具。
- **secs-simulator**（electron + secs4js）：SML 文件加载、按消息自动回复、JS 脚本自动回复、AutoFlow 编排（send/wait/delay/log 步骤链）。适合**回归场景编排**。
- **Python secsgem**（pip 安装）：可编程宿主，适合把集成测试写成 pytest 进 CI。
- 商业参照：Cimetrix EquipmentTest、PEER Group 等，仅作合规测试对标，不建议采购前先完成开源路线。

建议组合：CI 用 InMemory + secsgem 脚本；人工验收用 oca_gem_tool 或 secs-simulator 走场景表。

### 6.3 SEMI 合规视角（支撑 M2.1 与 M4 文档任务）

- E30 要求设备随附**能力合规矩阵**（Fundamental Requirements + Additional Capabilities 逐项 Supported/Not Supported），Gather Tech 等厂商文档给出了 22 项标准清单模板（状态机/S1F13 场景/S6F11/S9Fx/动态事件配置 S2F33-36/Trace S2F23/S5Fx/S2F41/S2F13-16+S2F29-30/S7Fx/时钟 S2F17,31/Spooling S2F43-44/Limits S2F45-48…）。M4 的"协议支持矩阵"直接采用此模板，避免自造格式。
- 行业经验（Einnosys/GEM300 Checklist）：**CEID 覆盖不全是最常见的合规审计失败项**——要求"每个控制器状态迁移都映射到采集事件"。M2.1 状态机接线时须同步核对 CEID 映射完整性（Secs 当前只有 Mode/Status 两个 CEID）。
- 处理状态模型（Process State：INIT/IDLE/SETUP/EXECUTING/PAUSE…）E30 只给示例不强制定义，但必须提供 `ProcessState`/`PreviousProcessState` 两个 SV 并在每次迁移时更新+触发事件、文档化迁移表（Harel 图）。当前 Secs 完全没有 Process 层状态——列入远期路线（见 6.4）。
- E37 定时器 T3/T5/T6/T7/T8 应可通过设备常量配置（已列入 N8/M3）。

### 6.4 远期路线图（本次不做，预留方向）

按 GEM300 家族标准的行业优先级，若产品要进入 300mm FAB，后续演进顺序：

1. **Spooling（S2F43/44 + 消息缓存重发）**——通信中断期间事件不丢，E30 Additional Capability 中价值最高；
2. **Limits Monitoring（S2F45-48）**——SV 越限自动报警，减少轮询；
3. **Process State Model 五层补全 + ProcessState/PreviousProcessState SV**；
4. E40 Process Job / E94 Control Job / E87+E90 物料追踪（仅当目标客户为 GEM300 FAB 时立项）；
5. E116 EDA 高频数据采集（与现有 TraceManager 合并设计采样引擎）。

---

## 七、立即可以开始的三件事

1. 建 `Secs.Tests` 工程 + `ISecsTransport`/`InMemorySecsTransport`，并接通一个外部模拟器（M0）；
2. 重写 `SecsHelper` 接收循环与 `ReplyAsync`（M1.1，收益最大），顺带补 Dispose（N7）；
3. 修 `UpdateSEMIMode` 断线状态丢失 + `DataBase.Save` 不落盘这两个一行级 Bug（N1/N3）。
