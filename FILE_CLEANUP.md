# 文件清理记录（2026-09-28）

| 处理 | 文件 | 证据与原因 |
| --- | --- | --- |
| 已删除 | `WinFormControl/WinFormControl/Class1.cs` | 文件只定义空的公开 `Class1`；图反向追踪为 0 个仓库内调用方，覆盖检查后直接读取源码并对全仓文本检索兜底；仓库内没有对该类的引用。删除后保留项目与解决方案。公开类型的外部消费者不可由本仓库证明，故这一步仍有外部兼容风险。 |
| 保留 | `WinFormControl/WinFormControl.csproj`、`WinFormControl/WinFormControl.sln` | 独立工程可能是预留或外部交付入口；本次没有足够证据删除工程身份。 |
| 保留 | `Vision/Vision/AI/Invi/InvIToolMgr.cs` 与 `Vision/DPLibrary_Halcon_F48/Tools/AI/Invi/InvIToolMgr.cs` | 内容完全相同，但分别属于两个独立项目，且各项目内有调用该类型的代码；合并前必须确定外部类型身份和程序集兼容。 |
| 保留 | `Packing Machine/Packing Machine/Config/Flow/StartupFlow/*.bak` | 属于配置/脚本备份，不是可据文件名判定无用的构建垃圾；保留恢复材料。 |
| 未触碰 | `Vision/VisionDemo/*_wpftmp.csproj`、`obj/`、`bin/` | VS / 构建临时文件受项目规则保护。 |
| 未触碰 | 根目录 `_tmp*.png`、既有历史架构文档 | 无法确认是否仍供用户复查，不能按名称清理。 |

清理判断只覆盖仓库内引用。`WinFormControl.Class1` 是公开类型，若有仓库外二进制或源码消费者，删除可能造成兼容变化；项目仍能编译不代表外部 API 兼容得到验证。

验证：`dotnet build WinFormControl/WinFormControl/WinFormControl.csproj -c Debug --no-restore -p:Platform=AnyCPU -v:q` 成功，0 警告、0 错误。首次沙箱内尝试因无法读取本机 Windows SDK 路径而失败；获准读取后同一命令通过。
