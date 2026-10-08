// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ConfigDirectoryIsolationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls.Configuration、DataManager.Controls.Models、DataManager.Workbench.Models、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using DataManager.Controls.Configuration;
using DataManager.Controls.Models;
using DataManager.Core.SQL.Definition;
using DataManager.Workbench.Models;
using Extension.Core.Conversion;
using System.IO;
using System.Security.Cryptography;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// T04 配置根路径隔离：验证 <see cref="ConfigRoot"/> seam 的四件事——
    /// ① 旧默认路径回归（生产行为不变）；② 页面模型的 FullPath 落在给定根下；
    /// ③ 两个会话（两个根）互不覆盖；④ 从临时根保存不会改动目录外的既有文件。
    /// 不依赖 WPF 宿主，直接构造页面模型。
    /// </summary>
    public class ConfigDirectoryIsolationTests : IDisposable
    {
        private readonly List<string> _tempDirs = [];

        /// <summary>
        /// 测试相关入口 NewRoot；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateTemporary、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        private ConfigRoot NewRoot()
        {
            var root = ConfigRoot.CreateTemporary();
            _tempDirs.Add(root.Directory);
            Directory.CreateDirectory(root.Directory);
            return root;
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            // 只清本测试自己造的临时目录；清理出错不覆盖断言抛出的原异常。
            foreach (var dir in _tempDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 旧默认路径必须逐字保持 &lt;BaseDirectory&gt;\Config，生产落盘位置不因本次改造而变。
        /// </summary>
        [Fact]
        public void Default_root_keeps_the_production_path()
        {
            Assert.Equal(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config"),
                ConfigRoot.Default.Directory);
        }

        /// <summary>
        /// 三个页面模型（SQL / Log / Excel）的 FullPath 都应落在传入的根下，且不再指向默认根。
        /// </summary>
        [Theory]
        [InlineData("SqlConnections.json")]
        [InlineData("LogConfig.json")]
        [InlineData("ExcelConfig.json")]
        public void Page_model_full_path_lives_under_the_given_root(string fileName)
        {
            var root = NewRoot();

            var actual = fileName switch
            {
                "SqlConnections.json" => new SqlPageModel<TestEntity>(root).FullPath,
                "LogConfig.json" => new LogPageModel(root).FullPath,
                _ => new ExcelPageModel<ExcelPerson>(root).FullPath,
            };

            Assert.Equal(Path.Combine(root.Directory, fileName), actual);
            Assert.StartsWith(root.Directory, actual!, StringComparison.OrdinalIgnoreCase);
            Assert.False(actual!.StartsWith(ConfigRoot.Default.Directory, StringComparison.OrdinalIgnoreCase),
                "页面模型不应再指向默认配置根");
        }

        /// <summary>
        /// 不同会话（不同根）各自独立：写进 A 的文件不会出现在 B，B 也读不到 A 的内容。
        /// </summary>
        [Fact]
        public void Two_sessions_do_not_overwrite_each_other()
        {
            var rootA = NewRoot();
            var rootB = NewRoot();
            Assert.NotEqual(rootA.Directory, rootB.Directory);

            var pathA = new SqlPageModel<TestEntity>(rootA).FullPath!;
            var pathB = new SqlPageModel<TestEntity>(rootB).FullPath!;
            Assert.NotEqual(pathA, pathB);

            JsonEx.Save(new List<SQLParam> { new() { Name = "会话A" } }, pathA);

            Assert.True(File.Exists(pathA), "A 会话的配置应落盘到 A 的临时根");
            Assert.False(File.Exists(pathB), "B 会话不应看到 A 写的文件");
            var readBack = JsonEx.TryRead<List<SQLParam>>(pathB);
            Assert.True(readBack is null || readBack.Count == 0);
        }

        /// <summary>
        /// 从临时根保存，不得改动目录外（默认根）既有文件的任何字节。
        /// 只比对保存前已存在的条目：允许别的测试新增文件，但既有文件被改/删即失败。
        /// </summary>
        [Fact]
        public void Saving_through_a_temp_root_leaves_outside_files_unchanged()
        {
            var defaultDir = ConfigRoot.Default.Directory;
            var before = SnapshotHashes(defaultDir);

            var root = NewRoot();
            JsonEx.Save(new List<SQLParam> { new() { Name = "隔离" } },
                new SqlPageModel<TestEntity>(root).FullPath!);

            var after = SnapshotHashes(defaultDir);
            foreach (var (path, hash) in before)
            {
                Assert.True(after.TryGetValue(path, out var now),
                    $"目录外文件被删除：{path}");
                Assert.Equal(hash, now);
            }
        }

        /// <summary>
        /// 空目录名必须被拒（防止误把「未指定」当成根，从而退回当前目录）。
        /// </summary>
        [Fact]
        public void Empty_directory_is_rejected()
        {
            Assert.Throws<ArgumentException>(() => new ConfigRoot("   "));
        }

        /// <summary>
        /// 测试相关入口 SnapshotHashes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exists、EnumerateFiles、HashData；具体条件和先后次序见方法体。
        /// </summary>
        private static Dictionary<string, string> SnapshotHashes(string dir)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(dir))
                return map;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                map[file] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            return map;
        }
    }
}
