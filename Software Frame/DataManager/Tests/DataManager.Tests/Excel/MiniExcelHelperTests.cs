// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MiniExcelHelperTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.MiniExcel。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.MiniExcel;

namespace DataManager.Tests.Excel
{
    /// <summary>
    /// MiniExcelHelper 功能测试（基于临时目录，无需数据库）
    /// </summary>
    public class MiniExcelHelperTests : IDisposable
    {
        private readonly string _dir;

        /// <summary>
        /// 初始化 MiniExcelHelperTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        public MiniExcelHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_miniexcel", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        /// <summary>
        /// 验证 CreateHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private MiniExcelHelper CreateHelper()
        {
            return new MiniExcelHelper
            {
                ExcelParam = new ExcelParam
                {
                    Name = "miniexcel",
                    FilePath = _dir,
                    FileName = "test.xlsx",
                    HasHeader = true,
                    ToolType = ExcelType.MiniExcel,
                }
            };
        }

        /// <summary>
        /// 验证 Sample 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToList、Select、Range；具体条件和先后次序见方法体。
        /// </summary>
        private static List<PersonRecord> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new PersonRecord { Name = $"M{i}", Age = 30 + i, CreateTime = new DateTime(2026, 1, i) })
            .ToList();

        /// <summary>
        /// 验证 WriteThenQuery_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、ToList、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WriteThenQuery_RoundTrip()
        {
            var helper = CreateHelper();
            helper.Write(Sample(3));

            var result = helper.Query<PersonRecord>().ToList();

            Assert.Equal(3, result.Count);
            Assert.Equal("M1", result[0].Name);
            Assert.Equal(31, result[0].Age);
        }

        /// <summary>
        /// 验证 Query_WithPredicate_Filters 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、ToList、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Query_WithPredicate_Filters()
        {
            var helper = CreateHelper();
            helper.Write(Sample(5));

            var result = helper.Query<PersonRecord>(x => x.Age > 33).ToList();

            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// 验证 QueryAsync_ReturnsData 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、QueryAsync、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task QueryAsync_ReturnsData()
        {
            var helper = CreateHelper();
            helper.Write(Sample(2));

            var result = await helper.QueryAsync<PersonRecord>();

            Assert.Equal(2, result.Count());
        }

        /// <summary>
        /// 验证 Add_ThrowsNotSupported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Throws、Add、Sample；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_ThrowsNotSupported()
        {
            var helper = CreateHelper();
            Assert.Throws<NotSupportedException>(() => helper.Add(Sample(1)[0]));
        }

        /// <summary>
        /// 验证 Update_ThrowsNotSupported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Throws、Update、Sample；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Update_ThrowsNotSupported()
        {
            var helper = CreateHelper();
            Assert.Throws<NotSupportedException>(() => helper.Update<PersonRecord>(x => true, Sample(1)[0]));
        }

        /// <summary>
        /// 验证 AsReader_Works 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、Equal、Count；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AsReader_Works()
        {
            var helper = CreateHelper();
            helper.Write(Sample(2));
            DataManager.Core.Excel.IExcelReader reader = helper;
            Assert.Equal(2, reader.Query<PersonRecord>().Count());
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
