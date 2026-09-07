using DataManager.Log;
using DataManager.Log.Definition;
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
        public void Create_UnsupportedType_ReturnsNull_DocumentedBehavior()
        {
            var manager = new LogManager();
            var param = CreateParam("a3");
            param.LogType = ELogType.NLog;

            // 当前实现：未支持的类型静默返回 null（列入优化计划，改为抛异常）
            Assert.Null(manager.Create(param));
        }

        [Fact]
        public void Config_CreatesAllLogs()
        {
            var manager = new LogManager();
            manager.Config(new List<LogParam> { CreateParam("b1"), CreateParam("b2") });

            Assert.Equal(2, manager.Count);
        }
    }
}
