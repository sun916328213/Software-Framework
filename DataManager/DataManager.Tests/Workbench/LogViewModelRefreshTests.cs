using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.Workbench.ViewModels;
using Extension.Struct;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// LogViewModel 刷新文件回归测试：日志文件无扩展名（logyyyyMMdd），刷新必须能找到。
    /// 背景：刷新曾用 *.txt 过滤，永远刷不出文件，文件查询/多行读取在工作台不可用。
    /// </summary>
    public class LogViewModelRefreshTests : IDisposable
    {
        private readonly string _dir;

        public LogViewModelRefreshTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_wb_refresh", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [Theory]
        [InlineData(ELogType.SeriLog)]
        [InlineData(ELogType.NLog)]
        [InlineData(ELogType.Log4Net)]
        public void RefreshFiles_FindsWrittenLogFile(ELogType logType)
        {
            var manager = new LogManager();
            var vm = new LogViewModel(manager)
            {
                FilePath = _dir,
                LogName = "workbench",
                SelectedLogType = logType,
            };
            vm.InitLogCommand.Execute();

            var log = manager["workbench"] ?? throw new InvalidOperationException("日志实例未创建");
            log.Add("hello-refresh", ELogLevel.Information);

            vm.RefreshFilesCommand.Execute();

            Assert.NotEmpty(vm.Files);
            Assert.NotNull(vm.SelectedFile);
            Assert.Contains(DateTime.Today.ToString("yyyyMMdd"), vm.SelectedFile);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
