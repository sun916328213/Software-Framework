global using Xunit;

// WPF 每进程只能有一个 Application 实例，宿主集成测试必须串行（与其它 Workbench.Tests 同口径）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]