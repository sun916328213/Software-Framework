# SecsGem.Workbench

Workbench 是最小模拟宿主，导航包含：

- **SECS/GEM**：Controls 提供连接、通用 SML 和消息监视器；已移除“S7 配方联调”页签及导入/读取/模板测试按钮。
- **配方管理**：Workbench 定义具体配方类型、示例、配方列表和手动切换。显示 PPID、版本、曝光、增益及速度；通信、JSON 转换和接收通知仍由 Controls 后台服务处理。

`WorkbenchRecipe` 是只读 record struct，通过 JsonRequired/Range 声明字段和校验范围。`WorkbenchRecipeSamples` 仅定义 DEMO-FAST、DEMO-PRECISE。App 将类型化集合、当前 PPID 读取函数及列表通知交给 `SecsWorkspace.ConfigureRecipeSource(...)`；页面构造和切换不创建网络连接。

## 外部 Host 联调

1. 在“SECS/GEM”手动配置 EQ 与外部 Host 模拟器的 HSMS 连接。配方读取是 **S7F5**，不是查询报警列表的 **S5F5**；S7F19 请求只有报头，不带 L[0] 正文。
2. Host 发送 **S7F3**（不是 S2F3）：正文为 `L[2] A PPID, B PPBODY`，PPBODY 为以下 UTF-8 JSON，四个字段必须齐全：

```json
{"version":1,"exposureUs":5000,"gain":1.5,"speedMmPerSec":100}
```

3. 测试新增时使用新 PPID，例如 `HOST-NEW`。发送 `DEMO-FAST` 是更新已有行，不会增加列表数量。
4. Runtime 存储原始字节并应答 S7F4；Controls 后台自动校验 JSON，更新宿主集合并通知“配方管理”刷新。无需打开 S7 页面或点击读取按钮。数据源绑定前已接收的正文会在绑定时补充同步。
5. 在“配方管理”选中目标行，点击“切换当前配方”。下载或选中行均不自动切换；同名更新也不会覆盖当前已切换的快照。
6. 若列表没有新增，检查“SECS/GEM → 消息监视器”：同步日志包含 `S7F3 PPID=...`，可区分未绑定数据源、JSON/参数校验失败、成功新增或更新。

ACKC7=0 只表示协议内存接受字节，**不表示 JSON 校验、持久化或工艺应用成功**。接收的 PPID 即使 JSON 无法解析也显示在列表中，标记“解析失败，不可切换”并显示错误详情，不覆盖原有有效数据或当前快照。重新下载合法正文后更新同一行并恢复切换能力。首字节为 `0x00` 的正文不是 JSON；本示例 JSON 以 UTF-8 字节 `0x7B`（`{`）开始，不自动剥离零字节或填充伪造参数。列表刷新发生在协议应答之后，此桥接不是生产配方服务的事务实现；详见 [RecipeIntegration.md](../SecsGem.Controls/RecipeIntegration.md)。

独立 Workbench 启动时，App 调用 Controls 的初始化入口，把 DEMO-FAST / DEMO-PRECISE 同时写入协议内存库。因此无需打开配方页或点击导入，Host 即可通过 S7F19 查询目录、S7F5 读取 JSON 二进制正文、S7F25 读取完整格式化正文；不必先向 EQ 下载 S7F23。S7F17 实际删除成功后，通过 Runtime 实例级通知删除宿主集合和解析失败记录；ACKC7 非零不删除列表。删除当前条目的目录记录不自动改变已应用快照，但它不能再次被选中切换。生产集成仍应使用统一配方服务，而不是将测试快照当作双库同步方案。S7F23 成功接收后也会自动新增/更新列表，无需打开 Controls 页面；源绑定前已有的格式化 PPID 会补入列表，S7F17 删除时一并清理。Workbench 现已配置下述 CCODE=1 业务映射：完整映射/校验后更新业务参数，允许手动切换；更新不改变已切换的当前快照。映射失败显示具体错误且禁止切换，不覆盖原有效参数；重传合法正文后恢复。通用 Controls 宿主若未配置映射，仍只显示目录项。自动配方变更事件仍未实现。

## S7F23 业务参数格式

以下是本 Workbench 与 Host 的约定，不是 SEMI 统一规定的命令含义。本示例选择 `ParameterBlockRecipeCodec<WorkbenchRecipe>`，接受一个 CCODE=1 参数块，四个参数必须完整、顺序固定，但接收不再固定 SECS 数值类型，支持 ASCII 数字和可转换的其他数值类型；这只是演示业务规格，不是 Runtime 或其他项目的限制。Workbench 通过 `WorkbenchRecipe.FormattedParameterNames` 声明属性顺序，App 配置型号和软件版本，Controls 负责双向转换和 DataAnnotations 校验。其他项目替换 `IFormattedRecipeCodec<T>` 可定义不同配方类型、多命令、字符串/数组/嵌套参数，不必修改 S7F23 / S7F25 处理器。

以下类型为默认发送编码，不是接收类型白名单；接收例如 A '1'、A '5000'、A '1.5'、A '100' 也可映射。转换按不变区域的数值规则进行，不接受非法文本、整数小数部分或溢出，不补 0、不截断向量；S7F25 仍回传原始类型。

| PPARM 顺序 | 业务属性 | 默认发送类型 | 校验 |
|---|---|---|---|
| 1 | Version | U4[1] | 正整数，1–2147483647；不再限定等于 1 |
| 2 | ExposureUs | U4[1] | 1–1000000 μs |
| 3 | Gain | F8[1] | 有限值，0–100 |
| 4 | SpeedMmPerSec | F8[1] | 有限值，>0 且 ≤1000 mm/s |

```sml
'S7F23' W
<L [4]
    <A [9] 'DEMO-Test'>
    <A [9] 'WORKBENCH'>
    <A [3] '1.0'>
    <L [1]
        <L [2]
            <U2 [1] 1>
            <L [4]
                <U4 [1] 1>
                <U4 [1] 5000>
                <F8 [1] 1.5>
                <F8 [1] 100>
            >
        >
    >
>
.
```

成功日志应包含“工艺参数映射及校验成功”，列表显示 Version=1、曝光=5000、增益=1.5、速度=100。之前的 CCODE=12 / A 'DEMO' 仅能通过协议结构校验，不能生成本业务配方。S7F24 ACKC7=0 仍只表示协议层接受，业务映射在应答后执行；映射失败会保留原始正文供 S7F25 诊断读取。MDLN/SOFTREV 仅记录，未实现机型兼容校验；协议库二进制与格式化正文独立，S7F23 不自动重写 S7F5 的原始字节。

## S7F25 / S7F26 核对

Host 请求为直接 ASCII PPID，例如 `'S7F25' W <A [9] 'DEMO-FAST'> .`。正常回复 S7F26 的 L[4] 应包含 PPID、WORKBENCH、1.0 和完整命令参数；值与上文示例一致。缺少格式化正文才回复 L[0]，不能凭 FastSIM 模板的 L[4] 标签判断线上数据。消息监视器的发送行会列出实际 PPID、元数据长度、命令数及完整 SML；仍有差异时提供这行日志与 FastSIM 的 Raw/SML 回包。

启动与合法 S7F3 同步会经编解码器生成格式化表示。当前仍是显式发布的内存快照，不会自动追踪宿主字典的任意直接编辑；本地更改后需通过发布入口更新。所有下载仍不自动选择或应用配方；生产统一数据源与事务提交边界见集成说明。

## 不同配方结构的回归用例

新增 `VariableRecipeSchemaTests`，使用独立 `ProfileRecipe` / `ProfileCodec`，不含 Version、曝光、增益、速度字段：

- HEADER 为 ASCII 命令码，三个参数：批次名、Boolean、二进制签名。
- STEP 为 ASCII 命令码，五个参数：步骤名、I4 偏移数组、F8 时间数组、Boolean、嵌套字符串标签列表。
- 步骤数覆盖 0 / 1 / 5 / 20，数组及标签长度随步骤变化，包括空数组、空列表和空二进制。
- 独立手写 SML 验证 Host 下载到不同业务类型的解码，再以完整 SML 比较 S7F25 回包；另覆盖本地发布、S7F3 转格式化、S7F23 更新、读取副本隔离和当前快照不变。

Runtime 的 `Different_Command_And_Parameter_Counts_Roundtrip_Without_Truncation` 另覆盖 0 / 1 / 2 / 3 / 17 条命令，各命令参数数不同；包含 ASCII、I4/F4 向量、Boolean、Binary、嵌套列表、U8 最大值、I2 和空 U2，比较完整 SML 以检查数量、类型和值未被截断。

以上均为新增测试用例，当前环境缺 Bash，**尚未执行，不代表已通过**。

## S9 业务场景

工作台启动已启用完整 SECS-II 正文 **1 MiB** 上限和配方下载等待 **30 秒**，不只是暴露发送接口：

- S7F1 声明的下载已超过容量：S7F2=2，不建立会话。
- S7F2=0 成功发出：等待同 PPID 的 S7F3；正常到达后取消等待，继续原下载/转换流程。
- 期限内未到达：自动 S9F13（MEXP=S7F3，EDID=PPID），不创建或切换配方；其他 PPID 不会误完成等待。
- 实际消息超限：执行处理器前自动 S9F11，不覆盖旧配方；同 PPID 已到达但被拒绝，不再追加“未收到”的 S9F13。
- 断线和宿主释放取消等待，重连不补发旧超时；迟到下载保持作为独立请求接收的兼容行为。

普通生产宿主默认不启用这些演示策略，可在断线时通过 `ConfigureProtocolScenarios` 显式配置。会话等待不是 T3，不混发 S9F9；下载和超时都不自动应用工艺。

实际 Host 操作步骤、流程状态、所有权表、测试入口及尚未取得的验证证据，见 [S9 业务使用与验收](../SecsGem.Runtime/Protocol/Messages/S9/README.md)。新增业务/回环用例尚未执行，不能视为真实设备验收。

## 所有权与验证

App 持有本地配方集合。Controls 借用集合并持有接收订阅，退订仅发生在 Workspace.Dispose，而非页面 Unloaded；回调使用独立字节副本，释放后丢弃排队通知。页面只释放自己的 VM，Runtime 由 App 关闭和释放。

```powershell
dotnet test "Software Frame/SecsGem/Tests/SecsGem.Workbench.Tests/SecsGem.Workbench.Tests.csproj" --filter "FullyQualifiedName~RecipeCommunicationTests|FullyQualifiedName~S7RecipeInteractionTests|FullyQualifiedName~WorkbenchCompositionTests"
dotnet test "Software Frame/SecsGem/Tests/SecsGem.Tests/SecsGem.Tests.csproj" --filter "FullyQualifiedName~S7ProcessProgramTests|FullyQualifiedName~ProcessProgramFacadeTests|FullyQualifiedName~LayeringTests"
```

回归覆盖：本地配方未下载即 S7F25 读取完整正文、替换为不同类型/双命令编解码器、S7F3 后格式化发布、晚绑定保留原格式化正文；S7F23 真实参数块转换/范围及类型校验/失败保留与重传恢复/手动切换、未配置映射时列表新增/同名更新/禁止切换、S7F25 读取、拒绝时不新增、晚绑定回填、删除及释放守卫；启动示例同时进入协议库、S7F19/S7F20 目录、S7F5/S7F6 正文、S7F17 拒绝时不改列表/成功时删除/全部删除时清理非法项、S7 UI 移除、列表/手动切换、先下载后绑定、解析失败仍显示但禁止切换、重传恢复、迟到/释放守卫。真实 127.0.0.1 HSMS 回环覆盖目录→二进制及格式化读取（独立 golden 字节）→下载→拒绝删除→指定删除→全部删除→空目录（不打开 Controls View）。回环测试不连接外部设备，也不等于用户模拟器验收。

当前执行工具缺 Bash，未取得构建/测试通过或红绿证据；WPF 主题/窄窗口和用户 Host 模拟器报文仍待验证。未连接真实硬件、未执行生产流程。
