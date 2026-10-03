// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：Log4NetHelperTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log.Definition、DataManager.Runtime.Log.Log4Net。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log.Definition;
using DataManager.Runtime.Log.Log4Net;

namespace DataManager.Tests.Log
{
    /// <summary>
    /// Log4NetHelper 写入与查询测试（与 Serilog 同格式，基于临时目录）
    /// </summary>
    public class Log4NetHelperTests : IDisposable
    {
        private readonly string _dir;

        /// <summary>
        /// 初始化 Log4NetHelperTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        public Log4NetHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_log4net", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        /// <summary>
        /// 验证 CreateHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
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

        /// <summary>
        /// 验证 ReadAllTextShared 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ReadToEnd；具体条件和先后次序见方法体。
        /// </summary>
        private static string ReadAllTextShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// 验证 Add_CreatesFileContainingMessage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Add、GetFiles、NotEmpty、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_CreatesFileContainingMessage()
        {
            using var helper = CreateHelper();
            helper.Add("hello-log4net", ELogLevel.Information);

            var files = Directory.GetFiles(_dir);
            Assert.NotEmpty(files);
            Assert.Contains("hello-log4net", ReadAllTextShared(files[0]));
        }

        /// <summary>
        /// 验证 Add_FileName_ContainsDateAndIsNotHidden 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Add、GetFileName、GetFiles、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_FileName_ContainsDateAndIsNotHidden()
        {
            using var helper = CreateHelper();
            helper.Add("naming-check", ELogLevel.Information);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            Assert.False(fileName.StartsWith("."), $"畸形文件名: {fileName}");
            Assert.Contains(DateTime.Today.ToString("yyyyMMdd"), fileName);
        }

        /// <summary>
        /// 验证 Add_MultipleLevels_AllMessagesPersisted 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Add、ReadAllTextShared、GetFiles、Contains；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Query_ByFileName_ParsesLevelAndInfo 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Add、GetFileName、GetFiles、Query；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Query_ByTimeRange_FindsTodayFile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Add、Query、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Query_ByTimeRange_FindsTodayFile()
        {
            using var helper = CreateHelper();
            helper.Add("range-check", ELogLevel.Warning);

            var infos = helper.Query(DateTime.Today, DateTime.Today);

            Assert.Contains(infos, x => x.Info != null && x.Info.Contains("range-check"));
        }

        /// <summary>
        /// 验证 Query_Multi_ByFileName_ReturnsRawLines 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Add、GetFileName、GetFiles、Query_Multi；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Query_Multi_ByFileName_ReturnsRawLines()
        {
            using var helper = CreateHelper();
            helper.Add("multi-check", ELogLevel.Information);

            var fileName = Path.GetFileName(Directory.GetFiles(_dir)[0]);
            var lines = helper.Query_Multi(fileName);

            Assert.Contains(lines, x => x.Contains("multi-check"));
        }

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Delete；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
