// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件只登记程序集级可见性。节点与共享逻辑（NetDeviceHelper、各 Net*Node）都是 internal，
// 其能力判定与依赖边界只能在同模块的测试工程里直接验证，故按本仓既有约定开放给测试程序集。
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FlowNodes.Workbench.Tests")]
