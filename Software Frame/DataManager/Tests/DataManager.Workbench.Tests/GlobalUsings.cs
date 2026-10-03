// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 直接导入的协作命名空间包括：Xunit、System.IO。导入不代表所有类型都会在运行时加载。
global using Xunit;
// 启用 UseWPF 后 WindowsDesktop 的隐式 using 集合不含 System.IO（File/Directory/Path），补一条保持原有用例可编译。
global using System.IO;

// 本套件所有用例共用「运行目录下同一批配置文件」：Config/*.json。
// 多个类并行跑时会互相删/写这些文件（如 SqlConnectionStoreTests 与 ControlSaveTests
// 抢 Config/SqlConnections.json），造成随机失败。统一串行执行换取确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
