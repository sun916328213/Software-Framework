// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
global using Xunit;

// 本套件共享同一个 STA Application 宿主（WPF 同一进程只允许一个 Application 实例），
// 禁止并行执行换取确定性；与无 UI 的 ScriptEditor.Tests 相互独立。
[assembly: CollectionBehavior(DisableTestParallelization = true)]