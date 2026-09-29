# ADR-0003 任务级上下文替代进程静态 imports

状态：已实施（P5）。

背景：`CustomRoslynHost` 的静态 `GlobalImports` 被任务加载/保存/回填反复追加，
任务之间互相污染，长期运行无法回收。

决策：引入 `ScriptEditor.Options.ScriptRuntimeOptions`（显式持有者）与
`ScriptProjectContext`（每任务一份：隐式 using + 引用基准目录）；
会话编译只读自己任务的集合；旧静态入口保留为兼容转发。
框架/宿主引用枚举下沉到 `RuntimeReferenceCache`，编辑器工作区与无头编译走同一入口；
用户引用经 `RoslynScriptReferenceResolver` 共享缓存（路径 + 写入时间命中）加载。

后果：多任务进程内隔离；`IScriptSession.ProjectContext` 为任务装配点；
动态脚本程序集仍常驻默认 ALC（不可卸载），硬隔离留给未来 ScriptWorker（见 ADR-0005）。
