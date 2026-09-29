# ADR-0004 V2 版本化持久化与镜像清单

状态：已实施（P5）。

背景：`.taskproj` 直接序列化 `ScriptItem` 集合，无版本号；引用为绝对路径字符串；
外部 `.cs` 镜像按序号/时间戳匹配，改名重排即错位。

决策：
- V2 DTO（`FormatVersion: 2`）：引用为结构化描述，工程目录下路径存相对路径；
  V1（无版本号旧形状）加载时经 `ScriptProjectMigration` 迁移；保存轮换 `.taskproj.bak`。
- 存储实现为 `ScriptProjectStore`（`IScriptProjectStore`，无头可用，无对话框）。
- 外部镜像附 `taskfiles.manifest.json`（文件名 ↔ ScriptId + 内容哈希），
  回填优先按标识匹配，无清单旧任务回退序号/时间戳兼容。

后果：旧文件可读；模型调整不再直接破坏文件兼容；VSCode 往返不依赖文件名顺序。
