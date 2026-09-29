# ADR-0001 四层拆分与依赖方向

状态：已实施（P1）。

背景：ScriptEditor 原为单一 WPF 库，Roslyn、运行状态、持久化、测试耦合在一起。

决策：拆为 Core / Runtime / Controls / Workbench（+ Tests），依赖只能自上而下：
Controls → Runtime → Core。Controls 依赖 Runtime 是合理的（“编辑 + 执行”复合控件），
红线是 Runtime 不得反向依赖 Controls；Core 不感知 WPF/Prism/RoslynPad/文件系统。

后果：`ScriptEditor.Controls.csproj` 引用 Runtime；Runtime 保持 `net9.0` 且无 WPF 控件引用；
第二脚本后端未来放在 `Runtime/Roslyn` 同级，不动 Core/Controls 契约。
