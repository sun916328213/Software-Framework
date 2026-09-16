using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.Log.Log4Net;
using DataManager.Log.NLog;
using DataManager.Log.SeriLog;

namespace DataManager.Tests.Log
{
    public class LogManagerTests
    {
        private static LogParam CreateParam(string name) => new()
        {
            Name = name,
            FilePath = Path.Combine(Path.GetTempPath(), "dm_tests_logmanager", name),
            Separator = "||",
            LogType = ELogType.SeriLog,
        };

        [Fact]
        public void Create_SeriLog_ReturnsSerilogHelper()
        {
            var manager = new LogManager();
            var log = manager.Create(CreateParam("a1"));

            Assert.IsType<SerilogHelper>(log);
            Assert.Equal(1, manager.Count);
            Assert.NotNull(manager["a1"]);
        }

        [Fact]
        public void Create_SeriLog_SetsParam()
        {
            var manager = new LogManager();
            var log = manager.Create(CreateParam("a2"));
            Assert.Equal("a2", log.Param.Name);
        }

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

        [Fact]
        public void Create_UnknownType_ThrowsNotSupported()
        {
            var manager = new LogManager();
            var param = CreateParam("a5");
            param.LogType = (ELogType)999;

            Assert.Throws<NotSupportedException>(() => manager.Create(param));
        }

        [Fact]
        public void Config_CreatesAllLogs()
        {
            var manager = new LogManager();
            manager.Config(new List<LogParam> { CreateParam("b1"), CreateParam("b2") });

            Assert.Equal(2, manager.Count);
        }

        [Fact]
        public void Create_MissingFilePath_ThrowsArgumentException()
        {
            var manager = new LogManager();
            var param = CreateParam("c1");
            param.FilePath = "  ";

            Assert.Throws<ArgumentException>(() => manager.Create(param));
        }

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
