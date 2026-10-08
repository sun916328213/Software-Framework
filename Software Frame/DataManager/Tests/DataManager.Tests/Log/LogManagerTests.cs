// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogManagerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、DataManager.Runtime.Log.Log4Net、DataManager.Runtime.Log.NLog。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using DataManager.Runtime.Log.Log4Net;
using DataManager.Runtime.Log.NLog;
using DataManager.Runtime.Log.SeriLog;

namespace DataManager.Tests.Log
{
    /// <summary>
    /// LogManagerTests 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public class LogManagerTests
    {
        /// <summary>
        /// 测试相关入口 CreateParam；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Combine、GetTempPath；具体条件和先后次序见方法体。
        /// </summary>
        private static LogParam CreateParam(string name) => new()
        {
            Name = name,
            FilePath = Path.Combine(Path.GetTempPath(), "dm_tests_logmanager", name),
            Separator = "||",
            LogType = ELogType.SeriLog,
        };

        /// <summary>
        /// 测试相关入口 Create_SeriLog_ReturnsSerilogHelper；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、IsType、Equal、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_SeriLog_ReturnsSerilogHelper()
        {
            var manager = new LogManager();
            var log = manager.Create(CreateParam("a1"));

            Assert.IsType<SerilogHelper>(log);
            Assert.Equal(1, manager.Count);
            Assert.NotNull(manager["a1"]);
        }

        /// <summary>
        /// 测试相关入口 Create_SeriLog_SetsParam；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_SeriLog_SetsParam()
        {
            var manager = new LogManager();
            var log = manager.Create(CreateParam("a2"));
            Assert.Equal("a2", log.Param.Name);
        }

        /// <summary>
        /// 测试相关入口 Create_NLog_ReturnsNLogHelper；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateParam、Create、IsType、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_NLog_ReturnsNLogHelper()
        {
            var manager = new LogManager();
            var param = CreateParam("a3");
            param.LogType = ELogType.NLog;

            var log = manager.Create(param);

            Assert.IsType<NLogHelper>(log);
            Assert.NotNull(manager["a3"]);
        }

        /// <summary>
        /// 测试相关入口 Create_Log4Net_ReturnsLog4NetHelper；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateParam、Create、IsType、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_Log4Net_ReturnsLog4NetHelper()
        {
            var manager = new LogManager();
            var param = CreateParam("a4");
            param.LogType = ELogType.Log4Net;

            var log = manager.Create(param);

            Assert.IsType<Log4NetHelper>(log);
            Assert.NotNull(manager["a4"]);
        }

        /// <summary>
        /// 测试相关入口 Create_UnknownType_ThrowsNotSupported；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateParam、Throws、Create；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_UnknownType_ThrowsNotSupported()
        {
            var manager = new LogManager();
            var param = CreateParam("a5");
            param.LogType = (ELogType)999;

            Assert.Throws<NotSupportedException>(() => manager.Create(param));
        }

        /// <summary>
        /// 测试相关入口 Config_CreatesAllLogs；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Config、CreateParam、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Config_CreatesAllLogs()
        {
            var manager = new LogManager();
            manager.Config(new List<LogParam> { CreateParam("b1"), CreateParam("b2") });

            Assert.Equal(2, manager.Count);
        }

        /// <summary>
        /// 测试相关入口 Create_MissingFilePath_ThrowsArgumentException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateParam、Throws、Create；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_MissingFilePath_ThrowsArgumentException()
        {
            var manager = new LogManager();
            var param = CreateParam("c1");
            param.FilePath = "  ";

            Assert.Throws<ArgumentException>(() => manager.Create(param));
        }

        /// <summary>
        /// 测试相关入口 Create_DuplicateName_Throws；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_DuplicateName_Throws()
        {
            var manager = new LogManager();
            manager.Create(CreateParam("d1"));

            Assert.Throws<InvalidOperationException>(() => manager.Create(CreateParam("d1")));
        }

        /// <summary>替换校验失败时保留调用方拥有的旧实例及其他模块实例。</summary>
        [Fact]
        public void ReplaceConfigured_Conflict_PreservesCompletePreviousSnapshot()
        {
            var manager = new LogManager();
            var owned = manager.Create(CreateParam("owned-old"));
            var external = manager.Create(CreateParam("external"));

            Assert.Throws<InvalidOperationException>(() => manager.ReplaceConfigured(
                new HashSet<string> { "owned-old" },
                new[] { CreateParam("owned-new"), CreateParam("external") }));

            Assert.Same(owned, manager["owned-old"]);
            Assert.Same(external, manager["external"]);
            Assert.Null(manager["owned-new"]);
            Assert.Equal(2, manager.Count);
        }

        /// <summary>成功替换只移除声明拥有的实例，并一次发布完整的新快照。</summary>
        [Fact]
        public void ReplaceConfigured_Success_PreservesExternalAndPublishesAllReplacements()
        {
            var manager = new LogManager();
            manager.Create(CreateParam("owned-a"));
            manager.Create(CreateParam("owned-b"));
            var external = manager.Create(CreateParam("external-stable"));

            manager.ReplaceConfigured(
                new HashSet<string> { "owned-a", "owned-b" },
                new[] { CreateParam("new-a"), CreateParam("new-b") });

            Assert.Null(manager["owned-a"]);
            Assert.Null(manager["owned-b"]);
            Assert.NotNull(manager["new-a"]);
            Assert.NotNull(manager["new-b"]);
            Assert.Same(external, manager["external-stable"]);
            Assert.Equal(3, manager.Count);
        }

        /// <summary>并发读取只能观察原子替换前或替换后的完整名称集合。</summary>
        [Fact]
        public async Task ReplaceConfigured_ConcurrentReaders_NeverObservePartialSnapshot()
        {
            var manager = new LogManager();
            manager.Create(CreateParam("external-concurrent"));
            manager.Create(CreateParam("left-a"));
            manager.Create(CreateParam("left-b"));
            var invalidSnapshots = new System.Collections.Concurrent.ConcurrentQueue<string>();
            using var cancellation = new CancellationTokenSource();

            var reader = Task.Run(() =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var names = manager.Items.Select(log => log.Param.Name).ToHashSet();
                    var isLeft = names.Contains("left-a") && names.Contains("left-b");
                    var isRight = names.Contains("right-a") && names.Contains("right-b");
                    if (names.Count != 3 || !names.Contains("external-concurrent") || isLeft == isRight)
                        invalidSnapshots.Enqueue(string.Join(",", names.OrderBy(name => name)));
                }
            });

            IReadOnlyCollection<string> owned = new[] { "left-a", "left-b" };
            for (var index = 0; index < 100; index++)
            {
                var useRight = index % 2 == 0;
                var prefix = useRight ? "right" : "left";
                manager.ReplaceConfigured(
                    owned,
                    new[] { CreateParam($"{prefix}-a"), CreateParam($"{prefix}-b") });
                owned = new[] { $"{prefix}-a", $"{prefix}-b" };
            }

            cancellation.Cancel();
            await reader;

            Assert.Empty(invalidSnapshots);
            foreach (var log in manager.Items.OfType<IDisposable>())
                log.Dispose();
        }
    }
}
