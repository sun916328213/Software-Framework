using Xunit;

namespace FlowEditorTest;

/// <summary>FlowLogContext 的运行序号计数器是进程级状态，相关集成测试串行隔离。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FlowLogContextCollection
{
    public const string Name = "FlowLogContext serial";
}
