using DataManager.Log.Definition;
using DataManager.Log.Log4Net;

namespace DataManager.Tests.Log
{
    /// <summary>
    /// Log4NetHelper 写入与查询测试（与 Serilog 同格式，基于临时目录）
    /// </summary>
    public class Log4NetHelperTests : IDisposable
    {
        private readonly string _dir;

        public Log4NetHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_log4net", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        private Log4NetHelper CreateHelper()
        {
            return new Log4NetHelper
            {
                Param = new LogParam
                {
                    Name = "test",
                    FilePath = _dir,
                    Separator = "||",
                    LogType = ELogType.Log4Net,
                }
            };
        }

        private static string ReadAllTextShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }

        [Fact]
        public void Add_CreatesFileContainingMessage()
        {
            using var helper = CreateHelper();
            helper.Add("hello-log4net", ELogLevel.Information);

            var files = Directory.GetFiles(_dir);
            Assert.NotEmpty(files);
            Assert.Contains("hello-log4net", ReadAllTextShared(files[0]));
        }

        [Fact]
        public void Add_FileName_ContainsDateAndIsNotHidden()
        {
            using var helper = CreateHelper();
            helper.Add("naming-check", ELogLevel.Information);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            Assert.False(fileName.StartsWith("."), $"畸形文件名: {fileName}");
            Assert.Contains(DateTime.Today.ToString("yyyyMMdd"), fileName);
        }

        [Fact]
        public void Add_MultipleLevels_AllMessagesPersisted()
        {
            using var helper = CreateHelper();
            helper.Add("msg-1", ELogLevel.Information);
            helper.Add("msg-2", ELogLevel.Reset);
            helper.Add("msg-3", ELogLevel.Error);

            var all = ReadAllTextShared(Directory.GetFiles(_dir)[0]);
            Assert.Contains("msg-1", all);
            Assert.Contains("msg-2", all);
            Assert.Contains("msg-3", all);
        }

        [Fact]
        public void Query_ByFileName_ParsesLevelAndInfo()
        {
            using var helper = CreateHelper();
            helper.Add("parse-me", ELogLevel.Error);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            var infos = helper.Query(fileName);

            Assert.NotEmpty(infos);
            var entry = infos.First(x => x.Info != null && x.Info.Contains("parse-me"));
            Assert.Equal(ELogLevel.Error, entry.Level);
            Assert.False(string.IsNullOrEmpty(entry.Time));
        }

        [Fact]
        public void Query_ByTimeRange_FindsTodayFile()
        {
            using var helper = CreateHelper();
            helper.Add("range-check", ELogLevel.Warning);

            var infos = helper.Query(DateTime.Today, DateTime.Today);

            Assert.Contains(infos, x => x.Info != null && x.Info.Contains("range-check"));
        }

        [Fact]
        public void Query_Multi_ByFileName_ReturnsRawLines()
        {
            using var helper = CreateHelper();
            helper.Add("multi-check", ELogLevel.Information);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            var lines = helper.Query_Multi(fileName);

            Assert.Contains(lines, x => x.Contains("multi-check"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
