# MX Component adapter

此目录包含可选的 MX Component 客户端 adapter。默认 MC 实现位于相邻的 `Slmp` 目录，两者通过上级目录中的 `IMcClientAdapter` 并存。

`CommunicationNew` 已引用用户提供的 MX Component V5 64 位 Interop DLL。`McParam.Backend = McClientBackend.MxComponent` 且不配置 `MxBridgeExecutablePath` 时，由内部 `MxComponentMcClientAdapter` 直接调用 `ActUtlType64Class`；第三方类型不会暴露到公共接口。

`MxLogicalStationNumber` 必须是 Communication Setting Utility 可注册的 0–1023；直接模式和桥接模式都会在创建 COM 对象前验证该范围。

定时器和计数器使用官方语义明确的地址：`TS/TC/TN` 分别表示定时器触点、线圈、当前值，`CS/CC/CN` 分别表示计数器触点、线圈、当前值。旧 `T/C` 地址解析继续按当前值兼容；公开枚举中的旧 `C` 数值因二进制兼容而保留并标记弃用，新代码不要使用。

地址值对象会保留设备规定的编号进制，因此 `D100`、`M100` 等十进制地址以及 `B10`、`X1A`、`WFF` 等十六进制地址在解析和重新输出后保持不变。

超时优先使用所有协议共有的 `TimeoutMs`。旧 `CommunicationTimeout` 未显式设置时自动跟随 `TimeoutMs`；已有代码若明确赋值，仍会保留该兼容覆盖行为。

安装并配置逻辑站后，可使用解决方案中的 `CommunicationNew.MxComponentSmokeTest` 做实机验收。工具通过主库公开 interface 工作，默认只读取 `D100`；密码从 `COMMUNICATIONNEW_MX_PASSWORD` 环境变量读取，写入必须额外提供 `--confirm-write`。参数解析和写保护由独立的 Windows x64 测试项目覆盖，具体命令见该项目 README。

如配置 `MxBridgeExecutablePath`，则改由独立的 .NET Framework 4.8 x64 `CommunicationNew.MxComponentBridge` 进程承载 COM 对象，主进程通过本机命名管道调用。直接模式较易部署，但同步 COM 调用会占用调用线程且无法中途取消；桥接模式可以在取消或卡死时终止整个桥接进程。桥接启动使用 `MxBridgeStartupTimeoutMs`，连接后的读取、写入和关闭使用 `CommunicationTimeout`；内部超时抛出 `TimeoutException`，调用方主动取消仍抛出 `OperationCanceledException`。两种 adapter 的并发 `DisposeAsync` 调用共享同一个释放任务，释放开始后不再接受新的连接或读写；桥接断开失败或最终释放超时也会回收子进程。桥接服务端通过 Windows DACL 关闭权限继承，并只授权启动它的当前 Windows 身份读写管道；随机管道名不是密码通道的唯一保护。

默认 `dotnet pack` 不会再分发三菱厂商 DLL。发布方确认拥有再分发权限后，可使用 `dotnet pack -p:IncludeMxComponentInteropInPackage=true`，两个 64 位 Interop DLL 会作为附加托管程序集进入包内的 `lib/net9.0`；MX adapter 仍会在连接前拒绝非 Windows 或非 x64 进程。源码项目引用会把 DLL 复制到本地构建输出，不受该开关影响。

两种模式都参考了 CM1 `MCHelper` 的逻辑站配置、`ActPassword` 和 `ReadDeviceBlock2` 用法。三菱 MX Component V5 Reference Manual 确认位设备块读起始编号必须是 16 的倍数，size 表示以 16 点为一字的字数，数据按低位优先存放；共享的 `MxComponentBitCodec` 会自动向下对齐、批量读取并裁剪到请求范围，因此直接模式和桥接模式不会产生不同的位偏移或负载格式。位写为保护请求范围外的相邻位，仍使用逐点写。字设备通过共享的 `MxComponentWordCodec` 显式转换 `short[]` 与 MC 小端负载，不再依赖 `Buffer.BlockCopy` 和宿主机端序。厂商返回码不会像旧实现一样被忽略；直接模式和桥接模式都会优先使用 `ActSupportMsg64Class.GetErrorMessage` 获取官方错误文本，再转换为 `MxComponentException`。桥接模式下密码只通过当前用户专用的本机命名管道发送，不出现在进程命令行中。

部署和实机验证必须具备：

- 已安装且已授权的 MX Component V5；
- 明确的 32/64 位控件选择和逻辑站配置；
- 目标 PLC 或官方模拟器上的读写、断线及错误码测试；
- 有界 IPC 队列、超时、进程退出和重启策略。
