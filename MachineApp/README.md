# MachineApp — 整机应用宿主

本工程是具体设备业务应用，也是 Software-Framework 的长期整机集成验证入口。与 `Software Frame/` 并列存放并纳入同一仓库；PLC、工艺流程、报警、SECS 定义及设备配置属于该业务宿主。

工程用 `IsIntegrationHost=true` 标记。根 `Software-Framework.sln` 的集成脚本检查该标记唯一指向本工程，并在模块 restore/build、逻辑测试、WPF 测试后再次构建本工程。

```powershell
./Build/Test-Integration.ps1 -CheckOnly
./Build/Test-Integration.ps1 -NuGetConfig C:/private/NuGet.Config -VendorDllRoot C:/private/Dll
```

MachineApp 只直接引用 WPFControls，由 WPFControls 统一引用并装配其他模块。部署守卫检查 WPFControls.dll、FlowNodes.dll 与 deps.json 中的条目，以及 PLC、报警、启动流程和 SECS CEID 配置。

这些检查验证跨模块构建与部署完整性。实际设备联机、运动、视觉算法和整机 UI 操作仍需要在现场或受控设备环境验收；不要直接运行自动流程来代替构建检查。


设备接入采用新 Device.Core 接口：宿主注册 Runtime 工厂，Device.ControlsModule 注册 Workspace 和页面。ProductionDeviceSession 从 Config 下的 Cameras.json、Lights.json、Rfids.json、Scanners.json 创建并连接生产设备；四类配置必须有效，缺失时直接失败，不回退模拟设备。关闭与初始化失败都会释放已创建设备。启动脚本同时保存于 .cs 与 StartupFlow.taskproj，修改时必须保持一致。旧视觉消费者已迁移并通过集成测试，真实硬件验收仍待完成；原有 05 启动脚本及嵌入副本存在语法错误，留待业务阶段单独修复。
