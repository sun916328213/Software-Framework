# MX Component adapter

此目录包含可选的 MX Component 客户端 adapter。默认 MC 实现位于相邻的 `Slmp` 目录，两者通过上级目录中的 `IMcClientAdapter` 并存。

MX Component V5 64 位 RCW 由内部 NuGet 包 `Mitsubishi.MxComponent.Interop`（5.0.0）提供，构建时自动复制到输出目录。`McParam.Backend = McClientBackend.MxComponent` 时，由内部 `MxComponentMcClientAdapter` 直接调用 `ActUtlType64Class`；第三方类型不会暴露到公共接口。

`MxLogicalStationNumber` 必须是 Communication Setting Utility 可注册的 0–1023；adapter 会在创建 COM 对象前验证该范围。

定时器和计数器使用官方语义明确的地址：`TS/TC/TN` 分别表示定时器触点、线圈、当前值，`CS/CC/CN` 分别表示计数器触点、线圈、当前值。旧 `T/C` 地址解析继续按当前值兼容；公开枚举中的旧 `C` 数值因二进制兼容而保留并标记弃用，新代码不要使用。

地址值对象会保留设备规定的编号进制，因此 `D100`、`M100` 等十进制地址以及 `B10`、`X1A`、`WFF` 等十六进制地址在解析和重新输出后保持不变。

超时优先使用所有协议共有的 `TimeoutMs`。旧 `CommunicationTimeout` 未显式设置时自动跟随 `TimeoutMs`；已有代码若明确赋值，仍会保留该兼容覆盖行为。

安装并配置逻辑站后，可使用解决方案中的 `CommunicationNew.MxComponentSmokeTest` 做实机验收。工具通过主库公开 interface 工作，默认只读取 `D100`；密码从 `COMMUNICATIONNEW_MX_PASSWORD` 环境变量读取，写入必须额外提供 `--confirm-write`。参数解析和写保护由独立的 Windows x64 测试项目覆盖，具体命令见该项目 README。
> ⚠️ 2026-09-23：`CommunicationNew.MxComponentSmokeTest` 工程已删除（实施说明 §3.4），
> 实机验收入口改用 `Communication.Workbench`。以下原描述保留备查。


> ⚠️ 2026-09-23：桥接模式已按《Software-Framework_Communication_Device重构删除实施说明》§3.3
> **整体删除** —— `CommunicationNew.MxComponentBridge` 工程、`MxComponentBridgeClientAdapter`、
> `MxComponentBridgeOptions`、`McParam.MxBridgeExecutablePath`、`McParam.MxBridgeStartupTimeoutMs`
> 以及 `McClientDevice` 的桥接分支均已移除，只保留 64 位 Interop 直接模式。
> 连接后的读取、写入和关闭统一使用 `CommunicationTimeout`；内部超时抛出 `TimeoutException`，
> 调用方主动取消仍抛出 `OperationCanceledException`。


Communication 不再自行打包三菱厂商 DLL：RCW 统一由内部 NuGet 包 `Mitsubishi.MxComponent.Interop` 提供并随构建复制到输出；MX adapter 仍会在连接前拒绝非 Windows 或非 x64 进程。源码项目引用会把 DLL 复制到本地构建输出。

实现参考了 CM1 `MCHelper` 的逻辑站配置、`ActPassword` 和 `ReadDeviceBlock2` 用法。三菱 MX Component V5 Reference Manual 确认位设备块读起始编号必须是 16 的倍数，size 表示以 16 点为一字的字数，数据按低位优先存放；共享的 `MxComponentBitCodec` 会自动向下对齐、批量读取并裁剪到请求范围，因此不会产生位偏移或负载格式差异。位写为保护请求范围外的相邻位，仍使用逐点写。字设备通过共享的 `MxComponentWordCodec` 显式转换 `short[]` 与 MC 小端负载，不再依赖 `Buffer.BlockCopy` 和宿主机端序。厂商返回码不会像旧实现一样被忽略；adapter 会优先使用 `ActSupportMsg64Class.GetErrorMessage` 获取官方错误文本，再转换为 `MxComponentException`。

部署和实机验证必须具备：

- 已安装且已授权的 MX Component V5；
- 明确的 32/64 位控件选择和逻辑站配置；
- 目标 PLC 或官方模拟器上的读写、断线及错误码测试；
