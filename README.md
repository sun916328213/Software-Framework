# C#框架

项目优化盘点与各模块待办见 [PROJECT_OPTIMIZATION_AUDIT.md](PROJECT_OPTIMIZATION_AUDIT.md)。

架构分级见 [ARCHITECTURE_GRADING.md](ARCHITECTURE_GRADING.md)，文件清理记录见 [FILE_CLEANUP.md](FILE_CLEANUP.md)。

## 构建入口（P13 收口）

- 仓库级：`Frame.sln`（唯一仓库级聚合入口）。
- 模块级（仅列保留项）：`DataManager/DataManager.sln`、`Communication/Communication.sln`、`Device/Device.sln`、`Extension/Extension.sln`、`MotionControl/MotionControl.sln`、`Vision/Vision.sln`、`FlowEditor/FlowEditor.sln`、`WPFCustomControls/WPFCustomControls.sln`、`WPFControl/WPFControl.sln`、`Secs/Secs.sln`、`NugetPackages/NugetPackages.sln`、`Packing Machine/Packing Machine.sln`、`WinFormControl/WinFormControl.sln`。
- `Secs/secs4net-base/secs4net.sln` 为第三方本地化副本，保持原样不治理。
- 历史跨模块聚合 `FlowEditor/Flow.sln` 已于 P13 删除，不再作为入口。
- 全部保留 Solution 统一 AnyCPU（`Debug|Any CPU` / `Release|Any CPU`），不新增 x64/x86 别名。
