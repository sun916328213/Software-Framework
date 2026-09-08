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
    }
}
