# ADR-0002 ScriptSession 为唯一运行状态源

状态：已实施（P3）。

背景：ScriptItem、ScriptEditorModel、ScriptEngine、ViewModel、View DP 各存一份
运行/编辑状态，靠 `_suppress...` 标记双向同步，切页与初始化竞态频发。

决策：运行状态（IsRunning/IsLooping/Output/StatusText/ExecutionTime/CompilationState、
取消令牌、编译缓存）只由 `IScriptSession` 持有；`ScriptItem` 为纯数据；
共享 Roslyn Runtime + 每脚本 Session，会话由运行时按脚本对象关联创建与释放。

后果：View/ViewModel 只读会话；`ScriptEditorModel` 不再保存运行状态；
`ScriptManagerService` 的 RunAll/StopAll/PreCompile 操作会话而非脚本对象方法。
