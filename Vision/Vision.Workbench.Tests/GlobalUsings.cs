global using Xunit;

// 本套件共享同一个 STA Application 宿主（WPF 同一进程只允许一个 Application 实例），
// 禁止并行执行换取确定性；与无 UI 的 Vision.Tests 相互独立。
[assembly: CollectionBehavior(DisableTestParallelization = true)]