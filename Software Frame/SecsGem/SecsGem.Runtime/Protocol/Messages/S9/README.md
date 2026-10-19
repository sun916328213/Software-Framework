# S9 业务使用与验收

**证据状态：已实现并新增用例，但尚未编译/执行。当前命令工具缺 Bash，不能声称测试通过、外部 Host 验收或 SEMI 认证。**

## 不是只提供发送接口

Runtime 接收路径已经接入业务策略；Workbench 启动明确配置 **完整 SECS-II 正文最多 1 MiB、配方传输会话等待 30 秒**。不会自动连接设备，没有专用 S9 测试页，外部 Host 通过原通信页面连接。现有消息日志显示许可、等待和超时。

```csharp
runtime.ConfigureProtocolScenarios(new SecsProtocolScenarioOptions
{
    MaxMessageBodyBytes = 1024 * 1024,
    ProcessProgramTransferTimeout = TimeSpan.FromSeconds(30)
});
```

生产宿主默认不启用这两项策略，必须按项目通信规格显式配置；只能断线时修改。该配方会话定义是当前宿主的明确业务选择，不推断所有机台必须采用相同会话规则。

## 业务流程一：允许下载、传输、再读取

1. Host 发 S7F1：`L[2] A PPID, U4 LENGTH`。
2. Runtime 原处理器校验结构；策略结合 PPID 和编码开销判断声明的下载是否能放入消息上限。
3. 已知无法接收：S7F2 `B[1] 2`（长度拒绝），不创建空配方、不建立等待、不发送 S9F11。
4. S7F2 `B[1] 0` **实际发送成功**后，建立该 PPID 的 30 秒会话；没有 W 位或应答失败不建立等待。
5. Host 在期限内发同 PPID 的 S7F3；等待立即结束，随后执行原存储校验。
6. 正常下载得到 S7F4 `B[1] 0`，Host 可用 S7F5/S7F6 读取相同正文；之后不应出现旧会话的 S9F13。

通信入库与 Workbench 参数映射仍分离：ACKC7=0 不表示业务转换、持久化或设备应用成功。下载、拒绝及超时均不自动切换当前配方。

## 业务流程二：Host 获准但没有发送预期配方

1. Host 为 `JOB-A` 完成 S7F1/S7F2 许可。
2. 30 秒内不发送 `JOB-A` 的 S7F3；下载 `JOB-B` 不算完成该等待。
3. Runtime 自动发送一次 S9F13，W=0：`L[2] A "S7F3", A "JOB-A"`。
4. 清除该等待，不生成空配方，不删除既有配方，不操作设备。
5. 迟到的 JOB-A 下载保持既有兼容行为：作为独立 S7F3 接收、校验和入库，不恢复已结束的会话，也不自动切换。
6. 同 PPID 重复获准重置该 PPID 的等待；不同 PPID 独立计时。

这不是 T3：S7F2 已经返回，超时的是跨事务的业务对话。普通发送的 T3 仍由 secs4net 管理，使用 S9F9，不混发 S9F13。

## 业务流程三：实际收到的消息超过机台接收上限

1. 正常 JOB-A 已入库。
2. Host 直接发送超限 S7F3，或声明很小但实际发送超大正文。
3. Runtime 在执行处理器之前计数完整 SECS-II 编码大小，发送 S9F11；B[10] 包含该超限消息的实际 MHEAD。
4. 不发送成功 ACK，不覆盖旧 JOB-A，不创建新配方。
5. Host 读取 JOB-A，仍得到之前的有效正文；连接继续可用。
6. 若该消息是已等待的同 PPID S7F3，即使被 S9F11 或 S9F7 拒绝，也不再谎报“没有收到”而追加 S9F13。

上限包含 List、ASCII PPID、Binary 等所有 Item 头与数据，不包含 HSMS 的四字节长度前缀和十字节报头。**恰好上限允许，多一字节拒绝。** 这不是 socket bufferSize，也不是仅 PPBODY 的字节上限。

计数不重新分配整个编码报文；ASCII/JIS8 使用依赖的实际 Encoding，数值向量按元素宽度，列表头按元素数。此守卫发生在 SDK 解码之后，不能代替网络入口的内存/DoS 防护或项目流控。

## 业务流程四：连接与宿主结束

断线、释放 Runtime 都取消旧会话；重连不恢复旧计时器，不向新连接补发旧超时。已进入传输层的发送不能承诺撤回，但取消后不再启动新的超时发送。页面关闭不擅自拥有 Runtime；应用生命周期仍由宿主负责。

| 对象 | 创建/拥有者 | 使用者 | 取消入口 | 释放入口 |
|---|---|---|---|---|
| 会话协调器 | Runtime | 接收路径、成功应答回调 | 非 Selected 状态 | Runtime.Dispose |
| 每个 PPID 等待及 CTS | 会话协调器 | 本次等待任务 | 同 PPID 到达/重新获准、断线、释放 | 等待任务 finally |
| 原接收 Item | SDK/接收上下文 | 同步校验和 PPID 提取 | 不跨等待持有 | 原接收生命周期 |
| S9 收到通知快照 | Transport | await 的 OnReply 订阅者 | Runtime 释放后忽略 | 通知结束后释放 |

## 其余 S9 场景

| 消息 | 实际使用场景 | 已接入行为 |
|---|---|---|
| S9F1 | Host 配错设备 ID，包括 S1F1 | SDK 自动发送原错误 ID 的 MHEAD，Runtime 不重复发送 |
| S9F3 | Host 使用机台不支持的 Stream，例如 S12F1 | 按注册表判断 Stream 不存在，发送 MHEAD |
| S9F5 | Stream 存在但 Function 未实现，例如 S1F251 | 发送 MHEAD，不混用 S9F7 |
| S9F7 | 已知请求结构错误，例如 S7F19 带 L[0] 正文 | 原处理器拒绝后发送 MHEAD，原消息 W=0/1 都可报告 |
| S9F9 | 设备主动 W=1 消息发生 T3 | 按具体超时 SecsMessage 捕获的 SHEAD 报告，不使用最后一条全局消息 ID |
| S9F11 | 实际报文超出配置接收能力 | 上述流程三自动触发；也保留显式发送 |
| S9F13 | 获准配方下载但预期 S7F3 未到达 | 上述流程二自动触发；其他项目对话仍需项目自己的会话定义 |

## 协议约束

- S9 是单向错误通知，W=0，不生成 S9F2/F4/F6/F8/F10/F12/F14。发送入口拒绝 S9 W=1。
- 对端的 S9 只校验和诊断，即使 W 位错误、正文非法、Function 未支持，也不再发送 S9；大小策略同样跳过 S9，避免错误循环。
- MHEAD/SHEAD 为十字节 HSMS data header，不含前四字节长度：0–1 原 Device ID（大端），2 原 W/Stream，3 Function，4–5 PType/SType，6–9 原 System Bytes（大端）。
- S9 自身使用独立 System Bytes；原事务相关性在正文内，不强求新旧编号相同。
- 六种报头型消息要求精确 B[10]；`S9HeaderData`、byte[]、B[10] Item 发送时复制。旧 `SS9F9` 保留兼容，调用方必须提供真正原事务的 ID。
- T3 用弱键按具体发送消息保存报头，查询返回副本；缺少报头不伪造；通知失败保留原 T3；对端 S9F9 不递归生成 S9F9。
- S9F13 为 L[2] ASCII MEXP、EDID；MEXP 非空，EDID 可空。兼容字段 `SS9F13.Order` 对应 MEXP，`Message` 对应 EDID，不是自由异常说明。
- S9F0 等未实现 Function 仅诊断，不回错误。

SDK 修正包括：SxF1 也检查设备 ID；只从合法 B[10] 提取原事务编号；S9F13 或畸形 S9 不得凭自身 System Bytes 结束无关 T3。Transport 在日志钩子同步复制 S9 再异步通知，未被事务接管的 S9 归还原 Item 内存。

## 外部 Host 手工验收（实际 Workbench）

1. 只连接测试 Workbench，不连接生产设备；设备 ID 与页面配置一致。
2. 正常：S7F1 PPID=`JOB-A`、LENGTH=62 → S7F2=0；30 秒内 S7F3 发送下面 UTF-8 JSON 的 Binary → S7F4=0；S7F5 读取核对正文；确认不自动切换。
3. 超时：S7F1 PPID=`JOB-TIMEOUT`、LENGTH=62 → 获准后不发送 S7F3；约 30 秒收到 S9F13（MEXP=S7F3、EDID=JOB-TIMEOUT），目录不应凭空增加配方。
4. 预拒绝：S7F1 声明 PPBODY=1 MiB，应得到 S7F2=2，因为完整消息还包含编码开销；不应出现 S9F13。
5. 实际超限：跳过 F1，直接用同 PPID 发送不少于 1 MiB 的 Binary 正文；S9F11 关联该次 MHEAD，随后读取旧 JOB-A 核对未被覆盖。
6. 清理：建立一个等待，然后断线/重连；等待超过 30 秒，不应在新连接收到旧会话超时。
7. 误配置/非法请求：错设备 ID → F1；S12F1 → F3；S1F251 → F5；S7F19 带 L[0] → F7。发送对端 S9 不应收到应答。

```json
{"version":1,"exposureUs":5000,"gain":1.5,"speedMmPerSec":100}
```

## 自动回归入口与证据边界

- `S9BusinessScenarioTests`：真实 Runtime+内存传输+手动时钟，覆盖许可/存储/读取、错 PPID、超时、迟到、重复许可、声明长度预拒绝、实际超限保持旧值、恰好上限/多一字节、非法到达不追加超时、断线/重连/释放、默认禁用、不同编码宽度和头长度边界。
- `S9RecipeTransferScenarioTests`：真实 localhost HSMS，Host 经 S7F1/F2→S7F3/F4→S7F5/F6 完成下载读取；模拟遗漏后推进业务时钟自动收 S9F13，再验证迟到下载；超大替换自动收 S9F11，再读旧配方。期望正文为独立手写字节，通信应答不是 mock。
- `S9WorkbenchScenarioTests`：实际 Controls→Workbench 类型转换与配方集合，覆盖正常下载更新待用参数、已应用快照不变、超时不增虚假行、超大替换保留旧参数、迟到下载仍待手动应用；STA 用例也尚未执行。
- `S9ProtocolTests`、`S9ProtocolIntegrationTests`：补充报文结构、S9 无应答、逐事务报头、S9F1/F7 真实链路与 S9F13 不干扰同号普通事务。
- 未完成证据：上述用例尚未运行；外部 Host 实测、WPF 操作、实际 T3 到期的套接字验收和真实硬件均未完成。内存传输的 T3 故障注入不能冒充实际等待 T3 的成功证据。

```powershell
dotnet test "Software Frame/SecsGem/Tests/SecsGem.Tests/SecsGem.Tests.csproj" --filter "FullyQualifiedName~S9BusinessScenario|FullyQualifiedName~S9RecipeTransferScenario|FullyQualifiedName~S9Protocol|FullyQualifiedName~S9_Unknown|FullyQualifiedName~SecsOrderCatalogTests"
dotnet test "Software Frame/SecsGem/Tests/SecsGem.Workbench.Tests/SecsGem.Workbench.Tests.csproj" --filter "FullyQualifiedName~S9WorkbenchScenario"
dotnet build "Software Frame/SecsGem/SecsGem.Workbench/SecsGem.Workbench.csproj"
```

尚无指定版本授权 E5/E30 原文，不能宣称全面标准符合性。仍不是生产级单一事务配方服务：权限、持久化、断电恢复、声明长度与正文的一致性、设备应用、其他类型会话及网络入口资源限制需按项目规格另外实现和验收。
