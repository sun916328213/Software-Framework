global using Xunit;
// 启用 UseWPF 后 WindowsDesktop 的隐式 using 集合不含 System.IO（File/Directory/Path），补一条保持原有用例可编译。
global using System.IO;

// 本套件所有用例共用「运行目录下同一批配置文件」：Config/*.json。
// 多个类并行跑时会互相删/写这些文件（如 SqlConnectionStoreTests 与 ControlSaveTests
// 抢 Config/SqlConnections.json），造成随机失败。统一串行执行换取确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
