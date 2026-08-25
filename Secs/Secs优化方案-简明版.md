# Secs 项目优化方案（简明版）

- 日期：2026-08-24
- 对象：`Secs/Secs`（设备侧 SECS/GEM 框架）
- 原则：**不换底层库、不推翻重来**，只做两件事——①对外接口收口到一个类；②修掉正确性 Bug。

---

## 一、现状一句话结论

底层 secs4net 可用，Order/VID 业务模型有价值；问题是**对外接口散落在 7 个类里**，业务方用起来"东一个西一个"，另有若干会现场出事的 Bug。

## 二、问题清单

### A. 对外接口分散（本次重点）

现在业务代码要碰这么多类才能干活：

| 要做什么 | 现在要用的类/成员 | 问题 |
|---|---|---|
| 连接/断开 | `SecsControl` 继承自 `SecsHelper` 的 `ConnectAsync/Close` | 入口藏在基类 |
| 发消息 | `SecsControl.SendItem()` | OK 但语义不明 |
| 更新变量 | `_VIDManager.UpdateVID()` | 公开属性穿透到内部对象 |
| 上报警 | `_VIDManager.UpdateAlarm()` | 同上 |
| 查状态/模式 | `_StatusManager.CurrentMode` | 同上 |
| 收 Host 命令 | 轮询 `_VIDManager.ActiveReceive` | 反模式 |
| 订阅事件 | 事件全挂在基类 `SecsHelper`（8 个） | 与方法不在一个类 |

另外：5 个 Manager 属性全部 public（`_VIDManager` 等），内部结构完全暴露；`ManagerBase.Dispose` 直接 `throw NotImplementedException`。

### B. 正确性 Bug（必须修）

| # | Bug | 后果 |
|---|---|---|
| B1 | `ReplyAsync` 用"缓存列表 + 10ms 轮询 + 按 S/F 匹配"找主消息 | 并发同 S/F 消息回错包、最坏等满 T3=45s |
| B2 | 接收分发用 `BeginInvoke` | Handler 异常被吞，故障不可见 |
| B3 | 断线时 `UpdateSEMIMode(Offline)` 被 `if(!IsConnected) return` 拦截 | 断线后模式永远停在 Online |
| B4 | `MessageOut` 日志前缀写成 `<--`（应为 `-->`)；收发日志方向难辨 | 排障困难 |
| B5 | `Send(SecsMessage, IsWait)` 忽略 IsWait 参数 | 发送语义不一致 |
| B6 | 多处 `throw ex` 重置堆栈 + 收到的 SecsMessage/Item 不 Dispose（secs4net 内存池要求 Dispose） | 异常难定位 / 长跑内存压力 |

## 三、业界参考（网络调研结论）

| 项目 | 做法 | 对本项目的启示 |
|---|---|---|
| **mkjeff/secs4net**（官方 ~640★） | 单一 `ISecsGem` 接口收口所有通信；官方应答模式 = `GetPrimaryMessageAsync` 循环里直接 `TryReplyAsync` | ①接口收口是正统做法；②B1 的轮询队列是反模式，照官方改 |
| **harrryhsu/SecsGem.NetCore** | 内核类 + 出站 `Function` 方法集 + 入站单一 `ISecsGemServerEventHandler` 回调接口 + `Feature` 数据聚合根 + `[SecsGemStream]` 特性注册 + 内置测试 Client + `StateChange.Accept` 可否决迁移 | **结构主参照**（见第四节）：出站语义化方法、入站单一接口替代事件/轮询、特性注册替代反射扫描、测试 Client 思路用于 Secs.Tests |
| **fateit/easysecsgem** | 三接口薄封装（状态/消息/日志），回调内直回 | 门面切面划分参照 |

> 结论：不引入第三方框架替换自研层。Order 类 + VID CSV 配置体系保留，只做门面化改造。

## 四、目标架构（结构参照 SecsGem.NetCore）

> 参照物：harrryhsu/SecsGem.NetCore。它的结构 = 一个内核类（出站 `Function` 方法集）
> + 业务实现一个回调接口（入站）+ 数据聚合根 `Feature` + 内置测试 Client。
> 本项目吸收其分层思想，但不照搬 AspNetCore 宿主与自研底层。

```
业务程序 ──────────────────►  SecsControl（唯一对外入口，对应它的 SecsGemServer）
                                   │ 内部私有持有（全部改 internal，对应它的 Feature 聚合根）
                                   ├─ VIDManager   ├─ StatusManager
                                   ├─ DeviceManager├─ ConnectManager
                                   └─ TraceManager └─ FactoryCollection
业务程序 ──────────────────►  实现 ISecsControlEvents（可选，对应它的 ISecsGemServerEventHandler）
```

### SecsControl 最终 API 清单（一个类说完所有事）

```csharp
public class SecsControl : IDisposable
{
    // ── 连接 ──
    Task<bool> ConnectAsync(SecsParam param);      // 或重载传 ip/port/deviceId
    Task DisconnectAsync();
    bool IsConnected { get; }

    // ── 初始化 ──
    void Config();                                  // 加载 VID/CEID/Report CSV

    // ── 数据（原 VIDManager 的对外能力）──
    void SetVid(string name, object value);
    object GetVid(string name);

    // ── 主动上报（命名参照它的 Function：语义化 + 带消息号）──
    Task<bool> S5F1AlarmReportSend(AlarmInfo alarm);
    Task<bool> S6F11EventReportSend(string ceidName);
    Task<SecsMessage> SendItemAsync(string order, params object[] data); // 兜底通用发送

    // ── 状态迁移（守卫式，参照它的 GoOnlineRemote/GoOnlineLocal）──
    ESecsMode Mode { get; }
    EDeviceStatus Status { get; }
    Task<bool> GoOnlineRemote();
    Task<bool> GoOnlineLocal();

    // ── 事件（收敛为 5 个以内；或升级为下方 ISecsControlEvents 接口）──
    event EventHandler OnConnected;
    event EventHandler OnDisconnected;
    event EventHandler<RemoteCommandArgs> OnRemoteCommand;   // 替代 ActiveReceive 轮询
    event EventHandler<string> OnError;

    void Dispose();
}

/// <summary>
/// 入站回调接口（参照 ISecsGemServerEventHandler，业务方实现这一个接口即可）
/// </summary>
public interface ISecsControlEvents
{
    Task OnRemoteCommand(RemoteCommandArgs e);   // S2F41，e.Return = HCACK 回码
    Task OnTerminalDisplay(TerminalDisplayArgs e); // S10F3/F5，e.Return = ACKC10
    Task OnStateChanged(StateChangeArgs e);      // e.Accept=false 可否决迁移
    void OnError(string message, Exception ex);
}
```

迁移策略：旧公开属性（`_VIDManager` 等）标 `[Obsolete]` 保一个版本，编译告警引导业务代码切换，下个版本删。

## 五、实施计划（3 步走）

> **进度（2026-08-24）：P1、P2 已完成；P3 协议级集成测试已完成（25/25 测试全绿）。**
> 已落地：ISecsControlEvents 回调接口（S2F41 免轮询）、SecsControl 门面 API
> （SetVid/S5F1AlarmReportSendAsync/S6F11EventReportSendAsync/GoOnlineRemoteAsync…）、
> 旧属性 [Obsolete] 迁移、接收循环直回+S9F7 兜底、断线状态修复、[SecsOrder] 特性注册工厂、
> DataBase.Save 原子落盘、AppendVID 类型强转修复、DeviceManager.Load 缺键保护。
>
> 集成测试：`Secs.Tests/Integration`（真实 TCP 回环 + 最小 HSMS Host 模拟器）验证
> ① Select 握手→S1F13→S1F14 应答；② S2F41→业务回调→S2F42(HCACK) 全链路；
> ③ 未实现指令自动 S9F7 兜底。注意：本地 fork 的 SType 取值与 E37 标准不同（Select.req=0x01）。

| 阶段 | 内容 | 工作量 | 出口标准 |
|---|---|---|---|
| **P1 门面收口** | ① 5 个 Manager 改 `internal`；② `SecsControl` 增加 `SetVid/GetVid/S5F1AlarmReportSend/S6F11EventReportSend/GoOnlineRemote` 等转发方法；③ 新增 `ISecsControlEvents` 回调接口，S2F41 远程命令从"ActiveReceive 轮询"改为接口回调（参照 SecsGem.NetCore 的 Handler 模式）；④ 旧属性加 `[Obsolete]`；⑤ 补 `ManagerBase.Dispose` | ~1 周 | Demo 只 using `Secs` 命名空间 + 一个类即可完成建连→上报→收命令全流程 |
| **P2 正确性修复** | ① B1：接收循环里直接 `TryReplyAsync`（照 secs4net 官方模式），删除 `_primaryMessages` 缓存和轮询；② B2：去 `BeginInvoke` 改顺序 await；③ B3：断线写 Offline 绕过门禁；④ B4/B5/B6：日志方向、IsWait、`throw ex`、Item Dispose；⑤ 工厂改 `[SecsOrder("SxFy")]` 特性注册（参照它的 `[SecsGemStream]`） | 1~2 周 | P1 的测试全绿 + 并发 50 条混合主消息全部正确回包 <100ms |
| **P3 测试完善** | ① Test 工程补协议级集成测试（模拟 Host：建链→S1F13→S2F41→S5F1→S6F11 全流程）；② CI 接 `dotnet test`；③ README 写明支持的消息矩阵 | 持续 | CI 绿 |

依赖关系：P1 → P2 → P3（P2 的 B3/B4 一行级修复可提前随手做）。

## 六、测试项目

已新建 `Secs.Tests`（xUnit, net9.0-windows, 引用 Secs 主工程），分层：

- `SecsOrderBaseTests` — Item 组包/解析纯逻辑测试（AddData、SecsItemToList 往返）
- `SecsOrderCatalogTests` — 特征化测试：程序集内 Order 类命名规范与存在性快照
- （P2/P3 再补）连接集成测试 — 用模拟 Host 走完整场景

运行方式：

```
cd Secs
dotnet test
```
