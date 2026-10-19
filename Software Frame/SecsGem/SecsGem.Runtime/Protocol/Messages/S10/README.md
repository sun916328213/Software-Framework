# S10 终端通知业务

## 范围与证据

本轮完成 S10F3/F4 单条通知、S10F5/F6 多行通知的接收、入队、异步显示、内存历史和本地阅读确认。不是操作员远程命令，不执行配方或硬件动作；没有为未约定用途的 S10F1/F2 等增加空接口。

**新增和更新测试尚未编译执行。命令工具缺 Bash，没有测试通过、WPF 交互验收或外部 Host 实测证据。**

## 指令格式

下列为注释版 SML；复制到模拟器前去掉右侧 `//`。源码处理器备注也使用同样格式。

```sml
'S10F3'W                      // Host→设备，单条通知，请求确认
<L [2]                        // TID、TEXT
    <B [1] 0x07>              // TID：终端编号，不混入文本
    <A [5] 'Hello'>           // TEXT：ASCII 文本
>
.
'S10F4'                       // 设备→Host
<B [1] 0x00>                  // ACKC10：0=成功接收入队，非已读
.
```

```sml
'S10F5'W                      // Host→设备，多行通知
<L [2]                        // TID、文本行列表
    <B [1] 0x07>              // TID：终端编号
    <L [3]                    // 按顺序显示三行，第二行为空
        <A [3] 'One'>         // 第 1 行
        <A [0] ''>            // 第 2 行：空行不能丢弃
        <A [3] 'Two'>         // 第 3 行
    >
>
.
'S10F6'                       // 设备→Host
<B [1] 0x00>                  // ACKC10：0=成功入队，1=不接受
.
```

这些 B/ASCII 是常见报文示例，**不是唯一允许的 SECS 类型**。S10TerminalMessageProcessor 仅复用 F3/F5 转换，不是额外业务层：两者均接受直接文本、JIS8/Unicode 字符串、数值、Boolean、Binary 和嵌套列表；数值向量用空格显示，列表递归用 CRLF 连接，Binary 按数值显示而不猜测字符编码。

只保留根 L[2]（终端标识和内容）、TID 可转换为当前 byte 标识的单值等基本检查。TID 可用 B、各数值类型或整数字符串，不截断溢出、不把负数/小数/无效文本默认为 0。无法转换返回 null，由现有接收链路报告 S9F7。此为项目兼容策略，不宣称是严格标准格式。

允许空文本、空数值向量或零行列表，作为空通知接收，不将其与缺失正文混同。不擅自追加未经项目规格确认的固定字符串长度、行数或 TID 范围；TID 保留全部 byte 值，当前统一显示，不承诺物理多终端路由。完整消息大小受宿主显式配置的接收上限约束，网络解码资源保护另行负责。

## 业务链路

1. Host 发送 F3/F5，Runtime 分离 TID 和正文，按实际值进行显示转换，不因类型与示例不同就拒绝。
2. Runtime 有界收件箱成功入队回 ACKC10=0；队列满或显示服务显式停用（TerminalDisplay=null）回 1。**不等待 UI Dispatcher、窗口关闭或操作员操作。**
3. 单个后台消费者有序分发通知，旧 ITerminalDisplay 实现仍兼容；带 TID 的新实现接收不可变 SecsTerminalMessage 快照。
4. Workbench 的 WpfTerminalDisplay 使用 BeginInvoke，最多打开一个非模态窗口，关闭后显示下一条；不是 ShowDialog，不阻塞协议或其他业务消息。
5. 点击窗口确定或页面“标记选中通知已读”才设置本地 IsRead；程序关闭窗口、释放服务、单纯成功分发都不算阅读，也不向 Host 追发第二个 ACK。
6. 窗口/显示服务失败记录 Failed 和错误文本，不把已经返回的 ACK 改成另一个应答或 S9F7。后续通知继续处理。

ACK 与窗口显示的物理时序不是强制串行屏障：后台显示可能早于 ACK 被 Host 收到，但 ACK 路径不依赖显示完成。Host 只能根据 ACK 得知接收入队结果，不能据此判断操作员看过内容。

## 界面与历史

现有 Controls“远程命令与 S10”区域增加终端历史表，显示编号、UTC 接收时间、TID、指令、正文、分发状态、已读与错误；选中通知可本地标记阅读。使用既有页面刷新计时器，无额外常驻 UI 订阅；页面卸载停止界面刷新，但不删除 Runtime 已接收通知。

Runtime 提供 `GetTerminalMessageHistory()`（最近 200 条独立状态快照）、`AcknowledgeTerminalMessage(id)` 和 `ReportTerminalDisplayFailure(id, reason)`。状态 Queued/Dispatched/Failed/Cancelled 与 IsRead 分离；Dispatched 只表示显示服务接受了请求，不保证屏幕呈现或操作员已读。历史是内存滚动记录，不持久化，旧未读记录也可能轮转，不可用作生产强审计或安全报警替代品。

默认无 WPF 时仍接收入队并通过后台 `TerminalMessageReceived` 转发文本；该旧事件现在线程为后台消费者，订阅者更新 UI 必须自行异步调度。使用自定义 TerminalDisplay 时仍由其负责展示，默认事件适配器不重复展示。

## 容量与失败

- Runtime 收件箱最多 64 条排队，另有至多 1 条正在分发；TryWrite 不等待空位，满时 ACK=1，不静默 DropOldest。
- WPF 显示队列独立限为 64 条（包括待 Dispatcher 执行和当前窗口）；已入 Runtime 队列后若 UI 侧资源不足，会留下 Failed 历史，不追改先前的 ACK。需要“屏幕资源预约成功才确认”的项目必须另行定义接收策略。
- Runtime 历史最多 200 条，不额外缓存 SDK Item 或池化内存。
- 空间限制是当前实现的资源策略，不是 SEMI 的固定数量要求。
- Host 是否重试由 Host 决定；当前不按相同文本或 TID 去重，避免吞掉合法重复通知。

## 生命周期与所有权

| 对象 | 创建/拥有者 | 借用者 | 停止/释放 |
|---|---|---|---|
| 收件箱、后台消费者、历史 | Runtime | 协议处理器 | Runtime.Dispose：禁止新接收，取消排队项，完成待处理标记 |
| 原请求 Item | SDK/原接收上下文 | 同步解析 | 不进入后台队列；仅保留复制出的字符串和标识 |
| ITerminalDisplay | 宿主 | Runtime 队列项 | Runtime 不擅自 Dispose 借用显示服务 |
| WpfTerminalDisplay、提示窗 | Workbench | Runtime、UI | Workbench 关闭时先停显示；自身 Dispose 只清除仍指向自己的注册、关闭自己的窗口并记录丢弃项 |
| 历史页面计时器 | Controls Workspace | 页面 | 既有订阅/卸载/Dispose 机制，不释放 Runtime |

断线**保留已经接受的通知与阅读状态**；重连不自动重放或重发旧 ACK。它们不是 S9 的跨事务等待。Runtime 释放后取消尚未分发消息；若宿主旧 Show 实现永久阻塞，void 接口无法强制中止它，Runtime 不同步等待该回调，调用方必须修正或释放自己的阻塞资源。Workbench 的新显示实现不等待操作员。

## 手工业务验收

仅连接测试 Workbench：

1. 连续发送 F3、F5；第一扇窗口不关，Host 应仍收到各自 F4/F6，并能继续读取 S7 目录。
2. F5 发送 One、空串、Two：窗口显示三行，标题显示 TID，正文不出现 TID 或字面 `/r/n`。
3. 关闭第一条后第二条才展示；点击确定只改变该条 IsRead，不产生线上 S10 回复。
4. TID 用 U1/U4/整数字符串、F5 直接发送文本、F3 发送列表均可接收；TID=256/-1/1.5、无效编号或缺少两项结构才报告 S9F7，不生成成功通知行。
5. 关闭显示服务/应用：不再出现迟到窗口，不把未确认通知自动标成已读；已接受但丢弃的窗口有失败记录。
6. 断开再连接：旧历史保留、无自动重播，新通知正常接收。

## 自动回归入口

- `S10TerminalScenarioTests`：可转换类型/直接文本/嵌套列表、基本结构与编号安全、TID 分离、CRLF/空行、历史快照/已读、显示阻塞时 ACK 不阻塞、满队列拒绝、释放取消、显示异常后恢复、关闭服务、断线重连及 200 条轮转。
- `S10TerminalWireTests`：真实 localhost HSMS，独立手写字节验证 F4/F6、System Bytes、阻塞显示时仍能确认和处理 S7F19、非法 TID 的完整 S9F7 MHEAD。
- `S10TerminalInteractionTests`：STA 真实非模态窗口，UI 尚未处理投递时协议已确认；单窗口顺序、显式确定才已读、释放窗口不伪造阅读以及历史命令状态。
- 更新原 `SecsProtocolIntegrationTests` 的终端测试：F5 示例使用常见行列表（非强制类型），异步等待分发，不再断言错误的 TID 混入和 `/r/n`。

```powershell
dotnet test "Software Frame/SecsGem/Tests/SecsGem.Tests/SecsGem.Tests.csproj" --filter "FullyQualifiedName~S10Terminal|FullyQualifiedName~Terminal_Message"
dotnet test "Software Frame/SecsGem/Tests/SecsGem.Workbench.Tests/SecsGem.Workbench.Tests.csproj" --filter "FullyQualifiedName~S10TerminalInteraction"
dotnet build "Software Frame/SecsGem/SecsGem.Workbench/SecsGem.Workbench.csproj"
```

本轮没有运行生产宿主、真实硬件或 Windows 对时操作。仍须在可用 .NET 9/WPF 环境执行上述测试及人工验收；不是指定版本标准认证结论。
