// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowLogContextCollection。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>FlowLogContext 的运行序号计数器是进程级状态，相关集成测试串行隔离。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FlowLogContextCollection
{
    public const string Name = "FlowLogContext serial";
}
