# Device（新栈唯一正式入口）

> P7（2026-09-24）：`Device.Core + Device.Runtime + Device.Controls + Device.Workbench`
> 为仓库唯一正式 Device 技术栈。旧 `Device/Device` 已从 `Frame.sln` / `Device.sln`
> 移除（solution 退场第一步），目录暂留过渡。
>
> P8（2026-09-24，进行中）：WPFControl 已**接入**新栈 —— `ProjectReference` 新增
> Core / Controls / Runtime，`WPFControlModule` 承担组合根（5 个工厂 + 5 个 Workspace
> + 5 个设备页导航注册），并落地旧 JSON → 新配置的一次性迁移器。
> **但 WPFControl 对旧 `Device/Device` 的 `ProjectReference` 尚未摘除**：旧
> `CamManagerService` / `LightControlManagerService` / `ScanManagerService` /
> `RFIDManagerService` 仍被 Packing Machine 的**运行时启动脚本**
> （`Config/Flow/StartupFlow/StartupFlow.taskproj`，对应 `<Compile Remove>` 的
> `04_初始化硬件.cs`）直接 `Resolve` 并读取 `Model.DataCollection` ⇒ 摘掉引用会让
> Packing 启动脚本在**运行时**编译失败，而编译期完全无感。
> **新代码禁止再引用旧 `Device` 程序集；旧目录仅做存量兼容，待迁移完成后整体删除。**

## 分层与依赖方向

```text
Device.Core        纯契约：设备契约、配置、结果模型（net9.0，无平台依赖）
    ↑                  不引用 WPF / Prism / Communication / 厂商 SDK
Device.Runtime     唯一运行时实现：厂商 SDK、Factory、运行时平台守卫
    ↑                  只依赖 Core（+ Communication），不新增 Prism 依赖
Device.Controls    可复用 WPF UI：仅依赖 Core（+ Extension / WPFCustomControls 等 UI 公共库）
    ↑                  不直接绑定 Runtime，保持 UI 与 Runtime 解耦
Device.Workbench   集成/调试宿主：组合 Controls + Runtime，承接必要宿主注册
```

依赖只能自上而下：`Workbench → Controls/Runtime → Core`。`DeviceModule`
（旧 Prism 模块）不迁入 `Runtime`；宿主若用 Prism，应在宿主/Workbench 层显式注册
`ICameraFactory` / `IFrameGrabberFactory` / `ILightControllerFactory` /
`IPrinterFactory` / `IRfidDeviceFactory` / `IScannerFactory` /
`ILabelPrintEngineFactory` 等。旧 `ManagerBase<T,TParam>` 批量管理模式不复制，
由宿主维护实例集合。

## 外部项目引用约定

- 默认只依赖 `Device.Core`（契约编程）。
- 需要真实硬件实现的宿主再依赖 `Device.Runtime`。
- 需要复用设备 UI 的宿主依赖 `Device.Controls`。
- `Device.Workbench` 为设备能力验证与调试入口。

## 平台策略（沿用 P6）

- 所有 Device 项目统一 `AnyCPU`；不设置 `PlatformTarget=x64`，不新增
  `x64/x86` solution platform。
- `Device.Runtime` 是 AnyCPU 类库，但部分厂商 SDK 仍可能仅支持 64 位进程，
  由运行时守卫给出可诊断错误，不通过 csproj 固定架构。

## 退场状态

- [x] P7-1 引用审计：旧 `Device/Device` 被 WPFControl（`using Device.*` /
  `assembly=Device` / `Resolve<CamManager…>`）与 Packing Machine
  （`DeviceModule` / 各 Manager / taskproj 脚本）真实引用；架构测试
  `ArchitectureDependencyTests` 钉住两处 `ProjectReference`。
- [x] P7-3 solution 切换：旧 `Device` 已移出 `Frame.sln` / `Device.sln`，
  新栈保留在 `05 Device`（Frame.sln）与 Device.sln。
- [x] P8-1 WPFControl 依赖切换与组合根注册：`WPFControl.csproj` 新增
  `Device.Core` / `Device.Controls` / `Device.Runtime` 直接引用；
  `WPFControlModule.RegisterTypes` 注册 5 个工厂 + 5 个 Workspace +
  `CameraView` / `LightView` / `RfidView` / `ScannerView` / `LabelPrintView`
  五个导航页（保持 AnyCPU，不新增 `PlatformTarget=x64`）。
- [x] P8-3 旧 JSON 配置兼容迁移：`WPFControl/Integration/Device/LegacyDeviceConfigImporter.cs`
  在模块初始化时执行一次性、幂等、非破坏式迁移
  （`CamParam` / `LightControlParam` / `RFIDParam` / `ScanParam` →
  `Cameras` / `Lights` / `Rfids` / `Scanners`）；旧文件永不改动，
  未知品牌（Xenics）、非 GigE 接口、语义不明确的触发组合一律拦截并告警，不猜值。
- [ ] P8-2 / P8-4 旧 Device 代码与引用退场 —— **受阻，待决策**。
  `CamManagerService` 等 4 个服务不是「仅服务旧 UI」：Packing Machine 的
  `StartupFlow.taskproj` 运行时脚本直接解析它们。此外 WPFControl 内还有 6 个
  计划外文件在用旧 `Device.*` 类型（`CustomControl` 的 `PrintService` /
  `PrintModel` / `LightControlService` / `PrintView`，`ProgramControl` 的
  `PrintManagerModel` / `PrintManagerViewModel` / `VisionInspectionItem*`），
  且旧 `Device.PrintSoftware.PrintInfo` 在新栈没有等价类型 ⇒ 摘引用前必须先
  决定这些调用点的处置方式。
- [ ] 宿主迁移（Packing Machine 调用点迁新栈）——暂缓，超出 P8 范围。
- [ ] P7-4 目录删除：仅在引用迁移完成、solution 可编译、关键测试通过后执行。
