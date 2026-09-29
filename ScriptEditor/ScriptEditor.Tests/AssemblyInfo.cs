using Xunit;

// P6：Core/Runtime 用例共享进程级 Roslyn 宿主与静态缓存，
// 禁止并行以保证确定性（与旧 TestRunner 串行语义一致）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
