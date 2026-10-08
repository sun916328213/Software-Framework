// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogViewModelRefreshTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、DataManager.Controls.Services。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// LogViewModel 刷新文件回归测试：日志文件无扩展名（logyyyyMMdd），刷新必须能找到。
    /// 背景：刷新曾用 *.txt 过滤，永远刷不出文件，文件查询/多行读取在工作台不可用。
    /// </summary>
    public class LogViewModelRefreshTests : IDisposable
    {
        private readonly string _dir;

        /// <summary>
        /// 初始化 LogViewModelRefreshTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        public LogViewModelRefreshTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_wb_refresh", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        /// <summary>
        /// 测试相关入口 RefreshFiles_FindsWrittenLogFile；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Execute、Add、NotEmpty、NotNull、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData(ELogType.SeriLog)]
        [InlineData(ELogType.NLog)]
        [InlineData(ELogType.Log4Net)]
        public void RefreshFiles_FindsWrittenLogFile(ELogType logType)
        {
            var manager = new LogManager();
            var vm = new LogViewModel(new LogWorkspace(manager));
            vm.Model.DataCollection[0].FilePath = _dir;
            vm.Model.DataCollection[0].Name = "workbench";
            vm.Model.DataCollection[0].LogType = logType;
            vm.InitLogCommand.Execute(null);

            var log = manager["workbench"] ?? throw new InvalidOperationException("日志实例未创建");
            log.Add("hello-refresh", ELogLevel.Information);

            vm.RefreshFilesCommand.Execute(null);

            Assert.NotEmpty(vm.Model.Files);
            Assert.NotNull(vm.Model.SelectedFile);
            Assert.Contains(DateTime.Today.ToString("yyyyMMdd"), vm.Model.SelectedFile);
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Delete；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
