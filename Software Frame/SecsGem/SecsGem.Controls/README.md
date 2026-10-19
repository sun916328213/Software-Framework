# SecsGem.Controls

通用 SECS/GEM 联调页由 Controls 提供，宿主通过 Prism 导航到 `SecsView`。页面自行合并 `Themes/PageStyles.xaml`，宿主提供 Material Design 主题资源。

- `Models/SecsPageModel`：CommunityToolkit ObservableProperty 页面输入、目录、选中项、运行状态和消息列表。
- `Services/SecsWorkspace`：共享页面模型，执行连接、配置、SML、变量、事件和报警流程；订阅运行时事件并将通知送回 UI Dispatcher。可见页面通过引用计数租约共享状态轮询。
- `ViewModels/SecsViewModel`：CommunityToolkit RelayCommand 转发流程并暴露 Model；保留旧属性绑定作为兼容入口。
- `Views/SecsView`：加载时取得轮询租约并监听消息滚动；卸载/释放时解除页面订阅。

`SecsWorkspace` 在模块内注册为单例，`SecsView` 为独立页面实例。释放 View/VM 不会释放共享 Workspace；释放 Workspace 只停止轮询并退订事件，不释放注入的 `SecsGemRuntime`。

Workbench 直接导航到 `SecsView`，不再有 `SecsWorkbenchView` 包装层；Workbench 自己的 `RecipeTestView` 显示为“配方管理”，提供列表与手动切换，没有通信编辑器。App 负责模块启动、导航与退出释放。示例 HCACK 处理器和 WPF 终端显示只在独立 Workbench 启用，通用 Controls 不替换宿主处理器。运行时所有者负责清除处理器和释放 Runtime。主窗 DataContext 由 Prism ViewModelLocator 注入。

背景、前景使用动态 Material Design 主题画刷，页头使用共享 `PageHeaderView`。命令随连接状态、输入及选中项刷新；端口为 1–65535，Device ID 为 0–32767。变量与配置可离线编辑，网络消息要求连接。VM 释放后禁止新操作；Workspace 释放后忽略后续操作和排队通知。

## S10 终端通知

“远程命令与 S10”区域提供最近 200 条终端历史和本地已读按钮；沿用页面状态轮询，不新增常驻订阅。Runtime 分离 TID 和 TEXT，不强制其 SECS 类型，按可转换值显示；列表保留 CRLF/空行；成功入队即回 ACK，不等待 Dispatcher 或操作员。

Workbench 显式安装 `WpfTerminalDisplay(runtime)`，单窗口按序非模态显示，点击确定才标记阅读。Runtime 不拥有注入的显示服务；Workbench 关闭时先 Dispose 显示服务，停用自身注册、关闭窗口并记录丢弃项，再释放 Runtime。页面卸载不停止已接收消息。显示失败保留状态，不追改已返回的 ACK。

详细容量策略、断线保留、已读/协议确认边界、格式及尚未执行的用例，见 [S10 业务说明](../SecsGem.Runtime/Protocol/Messages/S10/README.md)。

## S7 后台配方同步：无专用联调页签

已移除“S7 配方联调”页签和导入/读取/模板测试按钮。Host 使用外部模拟器；配方列表和切换入口统一在 Workbench 的“配方管理”。通用 SML、通信日志及后台服务仍保留，不因页面移除或 Unloaded 而停止接收。

`ConfigureRecipeSource<T>` 借用类型化集合和当前 PPID 读取函数，按宿主类型的 JsonRequired/DataAnnotations 校验；不依赖 Workbench 实体，也没有切换当前 PPID 的回调。绑定时补读已有二进制配方，避免先下载后装配漏刷新。

S7F23 通过 Runtime 的 `FormattedProcessProgramReceived` 成功通知进入 Dispatcher，再调用可选的 `formattedReceived` 宿主回调；仅 ACKC7=0 时更新列表。通过 `formattedCodec: IFormattedRecipeCodec<T>` 提供双向 `Encode(ppid, recipe)` / `Decode(program)`；接口不限定业务属性、单命令或四参数。项目可以实现多步骤、多 CCODE、字符串、数组或嵌套参数映射；Runtime 只验证四字段与命令容器；协议层 CCODE 不再限定 U2[1]，其他类型按原样存储/回传，这是兼容扩展。宿主业务编解码器仍须明确识别自己的命令与参数，不能因为协议接受就执行未知命令。`ParameterBlockRecipeCodec<T>` 是可选的简单实现，属性顺序、CCODE、MDLN 和 SOFTREV 由宿主配置，默认输出 U4 / F8；接收按目标业务属性转换实际值，允许 ASCII 数字及其他 SECS 数值类型，不再固定要求 U4/F8。ASCII 命令编号也可用于简单参数块，不改写协议存储的原始类型；原 `formattedParameters` / `formattedCommandCode` 参数作为这个简单实现的兼容入口，不是协议层的通用业务限制。再复用宿主必填/范围校验，全部成功后才更新集合。不会按反射顺序猜参数，也不会补默认值或截断数值。失败保留有效参数和当前快照，但该行禁止切换；合法重传后恢复。未配置映射时仅显示目录，不伪造类型化参数。绑定时补读已有格式化 PPID；已删除项的迟到通知忽略，Dispose 退订并清空回调。Workbench 已声明版本/曝光/增益/速度的 CCODE=1 参数块映射，详见 Workbench README；成功映射不代表设备执行，也不自动更新协议库的二进制正文。

配置双向编解码器后，`ImportHostRecipes()` 先编码并验证所有配方的二进制与格式化表示，再写入 Runtime；S7F25 可直接读取本地示例，无须先做 S7F23。合法 S7F3 下载同步业务集合时，也通过同一个编解码器发布格式化表示；无法编码时报告失败，不覆盖旧有效格式化数据。晚绑定已有两种正文时，不用旧二进制覆盖已接收的格式化正文。未配置编解码器时不会伪造格式化正文，S7F25 仍可读取此前 S7F23 保存的原始正文。

S7F26 发送日志增加实际根列表、PPID、元数据长度和命令数，并保留完整 SML；不要以 FastSIM 模板树的 L[4] 标签代替原始回包。未找到格式化正文是 L[0]，不是四个空字段。

独立 Workbench 启动时通过 `ImportHostRecipes()` 将示例的两种表示写入同一个 Runtime 内存库；通用 Controls 不会自行创建业务示例。Host 使用 S7F19 查询目录、S7F5 读取正文，S5F5 不用于配方读取。

S7F17 的删除通知基于处理器实际 ACKC7=0，而不是“收到请求”就删 UI。实例级 Runtime 通知经 Dispatcher 删除宿主集合、清除对应解析失败状态并刷新列表；未知 ID 整批拒绝时保持列表不变。空列表成功删除全部，不自动切换当前快照。Workspace.Dispose 退订该通知。

S7F3 接收后先复制独立正文快照，经 Dispatcher 验证并更新宿主集合、通知列表刷新。新 PPID 新增，同 PPID 更新原行；通过接收结果回调把解析失败的 PPID 和错误也交给宿主显示，失败项禁止切换且保留原有效数据。合法重传清除错误，释放后的通知丢弃。消息监视器记录具体 `S7F3 PPID=...` 的成功/失败原因。S2F3 不作为配方下载处理。

**Workbench 只定义配方类型、示例、配方管理和手动切换**，见 [Workbench 说明](../SecsGem.Workbench/README.md)。同步不会改变当前 PPID 或当前配方快照，更新库不等于应用设备配方。同步仍在协议应答之后执行，ACKC7=0 只代表原始字节接收，不是生产事务校验成功。原有程序化导入/读取和模板 API 保留，不再由页面提供按钮。

PPID 为 ASCII 标识；`001001` 不是 U4，必须保留前导零。Workbench 选中行仅代表待切换项，必须点击切换按钮才更新当前配方，不自动上报事件。真实提交和应用需由业务服务按通信规格上报 S6F11/S6F12；当前尚未自动接入 PPChange。详见 [S7 协议说明](../SecsGem.Runtime/Protocol/Messages/S7/README.md)。

| 对象 | 所有者 | 释放边界 |
|---|---|---|
| Runtime / 内存配方库 | 宿主容器 | 宿主 Shutdown/Dispose；页面不释放 |
| Controls Workspace / 只读快照 | Controls 容器 | Workspace 退订事件；View/VM 释放自己的订阅 |
| 宿主配方集合 / 当前选择 | 宿主 App | Controls 只借用；Dispose 清除回调，不释放集合 |
| 通用 JSON / S7 通信流程 | Controls Workspace | 页面不另建通信会话；退出时 Workspace 退订并清除回调 |

当前数据源仍为 Runtime 内存库，不会自动读取 MachineApp 配方。生产接入建议见 [RecipeIntegration.md](RecipeIntegration.md)，不要复制 App 集合形成第二套配方库。

## 验证

`Tests/SecsGem.Workbench.Tests` 包含真实按钮绑定、共享模型、释放边界与主题资源检查；`S7RecipeInteractionTests` 验证 Workbench 手动切换与 Controls 通信边界，`RecipeCommunicationTests` 验证通用序列化和 S7 组包。协议及分层回归位于 `Tests/SecsGem.Tests`，`ProcessProgramFacadeTests` 覆盖目录、预览及独立二进制读取副本。

当前环境缺 Bash，相关改动尚未编译或执行测试；真实 Host、深浅主题及窄窗口交互需另行验收。
