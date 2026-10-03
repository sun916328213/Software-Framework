// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
global using Xunit;

// 本套件共享同一个 STA Application 宿主（WPF 同一进程只允许一个 Application 实例），
// 禁止并行执行换取确定性；与无 UI 的 Vision.Tests 相互独立。
[assembly: CollectionBehavior(DisableTestParallelization = true)]