using Xunit;

namespace FlowEditorTest;

/// <summary>FlowLogContext.FileSink 是进程级可变钩子，相关集成测试必须串行隔离。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FlowLogContextCollection
{
    public const string Name = "FlowLogContext serial";
}
