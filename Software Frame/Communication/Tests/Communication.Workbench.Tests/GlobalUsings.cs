// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
global using Xunit;

// 本套件共享同一个 STA Application 宿主（WPF 同一进程只允许一个 Application 实例），
// 禁止并行执行换取确定性；与无 UI 的 Communication.Tests 相互独立。
[assembly: CollectionBehavior(DisableTestParallelization = true)]