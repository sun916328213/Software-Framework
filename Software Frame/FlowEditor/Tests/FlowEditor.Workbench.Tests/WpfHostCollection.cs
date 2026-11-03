using Xunit;

namespace FlowEditor.Workbench.Tests;

// 所有借用同一Application的类在同一集合结束后释放STA；失败不吞异常、不强停进程。
[CollectionDefinition("FlowEditorWpf", DisableParallelization = true)]
public sealed class WpfHostCollection : ICollectionFixture<WpfHostFixture> { }

public sealed class WpfHostFixture : IDisposable
{
    public void Dispose() => WpfTestHost.Shutdown();
}
