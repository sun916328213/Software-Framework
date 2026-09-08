using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.Log.SeriLog;
using Extension.Struct;

namespace DataManager.Tests.Log
{
    /// <summary>
    /// SerilogHelper 写入与查询测试（基于临时目录）
    /// </summary>
    public class SerilogHelperTests : IDisposable
    {
        private readonly string _dir;

        public SerilogHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_log", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        private SerilogHelper CreateHelper()
        {
            return new SerilogHelper
            {
                Param = new LogParam
                {
                    Name = "test",
                    FilePath = _dir,
                    Separator = "||",
                    LogType = ELogType.SeriLog,
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
            helper.Add("hello-serilog", ELogLevel.Information);

            var files = Directory.GetFiles(_dir);
            Assert.NotEmpty(files);
            Assert.Contains("hello-serilog", ReadAllTextShared(files[0]));
        }

        [Fact]
        public void Add_FileName_ContainsDateAndIsNotHidden()
        {
            using var helper = CreateHelper();
            helper.Add("naming-check", ELogLevel.Information);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            // 修复前：文件名为 ".txt20260907.txt"（以点开头的畸形名称）
            Assert.False(fileName.StartsWith("."), $"畸形文件名: {fileName}");
            Assert.Contains(DateTime.Today.ToString("yyyyMMdd"), fileName);
        }

        [Fact]
        public void Add_MultipleCalls_AllMessagesPersisted()
        {
            using var helper = CreateHelper();
            helper.Add("msg-1", ELogLevel.Information);
            helper.Add("msg-2", ELogLevel.Warning);
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
            helper.Add("parse-me", ELogLevel.Warning);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            var infos = helper.Query(fileName);

            Assert.NotEmpty(infos);
            var entry = infos.First(x => x.Info != null && x.Info.Contains("parse-me"));
            Assert.Equal(ELogLevel.Warning, entry.Level);
            Assert.False(string.IsNullOrEmpty(entry.Time));
        }

        [Fact]
        public void Query_ByTimeRange_FindsTodayFile()
        {
            using var helper = CreateHelper();
            helper.Add("range-check", ELogLevel.Error);

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

        [Fact]
        public void Dispose_ReleasesFileLock()
        {
            var helper = CreateHelper();
            helper.Add("lock-check", ELogLevel.Information);
            helper.Dispose();

            // 释放后可用独占方式打开（证明句柄已释放）
            var file = Directory.GetFiles(_dir)[0];
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            Assert.True(fs.Length > 0);
        }

        [Fact]
        public void Add_ParallelWrites_AllPersisted()
        {
            using var helper = CreateHelper();
            const int count = 50;
            Parallel.For(0, count, i => helper.Add($"parallel-{i:00}", ELogLevel.Information));

            var all = ReadAllTextShared(Directory.GetFiles(_dir)[0]);
            for (int i = 0; i < count; i++)
                Assert.Contains($"parallel-{i:00}", all);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
