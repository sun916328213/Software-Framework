# Device（新栈唯一正式入口）

> P7（2026-09-24）：`Device.Core + Device.Runtime + Device.Controls + Device.Workbench`
> 为仓库唯一正式 Device 技术栈。旧 `Device/Device` 已从 `Frame.sln` / `Device.sln`
> 移除（solution 退场第一步），目录暂留过渡——WPFControl / Packing Machine
> 对旧 `CamManager` / `LightControlManager` / `ScanManager` / `RFIDManager` /
> `DeviceModule` 的调用点尚未迁移，删除目录将中断编译。**新代码禁止再引用旧 `Device`
> 程序集；旧目录仅做存量兼容，待迁移完成后整体删除。**

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
- [ ] 宿主迁移（WPFControl / Packing Machine 调用点迁新栈）——暂缓，超出 P7 范围。
- [ ] P7-4 目录删除：仅在引用迁移完成、solution 可编译、关键测试通过后执行。
