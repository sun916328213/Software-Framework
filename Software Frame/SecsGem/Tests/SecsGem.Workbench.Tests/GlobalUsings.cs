// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
global using Xunit;

// WPF 每进程只能有一个 Application 实例，宿主集成测试必须串行（与其它 Workbench.Tests 同口径）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]