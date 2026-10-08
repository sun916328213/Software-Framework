// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：EPPlusHelperTests、Level、TypedRecord。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.EPPlus。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.EPPlus;

namespace DataManager.Tests.Excel
{
    /// <summary>
    /// EPPlusHelper 功能测试（基于临时目录，无需数据库）
    /// </summary>
    public class EPPlusHelperTests : IDisposable
    {
        private readonly string _dir;

        /// <summary>
        /// 初始化 EPPlusHelperTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        public EPPlusHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_epplus", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        /// <summary>
        /// 测试相关入口 CreateHelper；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private EPPlusHelper CreateHelper(string fileName = "test.xlsx")
        {
            return new EPPlusHelper
            {
                ExcelParam = new ExcelParam
                {
                    Name = "epplus",
                    FilePath = _dir,
                    FileName = fileName,
                    HasHeader = true,
                    ToolType = ExcelType.EPPlus,
                }
            };
        }

        /// <summary>
        /// 测试相关入口 Sample；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList、Select、Range；具体条件和先后次序见方法体。
        /// </summary>
        private static List<PersonRecord> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new PersonRecord { Name = $"P{i}", Age = 20 + i, CreateTime = new DateTime(2026, 1, i) })
            .ToList();

        /// <summary>
        /// 测试相关入口 WriteThenQuery_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Sample、Write、ToList、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WriteThenQuery_RoundTrip()
        {
            var helper = CreateHelper();
            var data = Sample(3);
            helper.Write(data);

            var result = helper.Query<PersonRecord>().ToList();

            Assert.Equal(3, result.Count);
            Assert.Equal("P1", result[0].Name);
            Assert.Equal(21, result[0].Age);
            Assert.Equal("P3", result[2].Name);
        }

        /// <summary>
        /// 测试相关入口 Query_WhenFileMissing_ReturnsEmpty；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Empty、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Query_WhenFileMissing_ReturnsEmpty()
        {
            var helper = CreateHelper("missing.xlsx");
            Assert.Empty(helper.Query<PersonRecord>());
        }

        /// <summary>
        /// 测试相关入口 Query_WithPredicate_Filters；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、ToList、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Query_WithPredicate_Filters()
        {
            var helper = CreateHelper();
            helper.Write(Sample(5));

            var result = helper.Query<PersonRecord>(x => x.Age > 23).ToList();

            Assert.All(result, x => Assert.True(x.Age > 23));
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// 测试相关入口 Add_AppendsWithoutOverwritingLastRow；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、Add、ToList；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_AppendsWithoutOverwritingLastRow()
        {
            var helper = CreateHelper();
            helper.Write(Sample(2));
            helper.Add(new PersonRecord { Name = "APPEND", Age = 99, CreateTime = new DateTime(2026, 2, 2) });

            var result = helper.Query<PersonRecord>().ToList();

            // 修复前：追加会覆盖最后一行（P2 变成 APPEND，总行数不变）
            Assert.Equal(3, result.Count);
            Assert.Equal("P2", result[1].Name);
            Assert.Equal("APPEND", result[2].Name);
        }

        /// <summary>
        /// 测试相关入口 AddRange_AppendsWithoutOverwriting；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、AddRange、Select；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AddRange_AppendsWithoutOverwriting()
        {
            var helper = CreateHelper();
            helper.Write(Sample(2));
            helper.AddRange(Sample(2).Select(x => new PersonRecord { Name = x.Name + "X", Age = x.Age, CreateTime = x.CreateTime }));

            var result = helper.Query<PersonRecord>().ToList();

            Assert.Equal(4, result.Count);
            Assert.Equal("P2", result[1].Name);
            Assert.Equal("P1X", result[2].Name);
            Assert.Equal("P2X", result[3].Name);
        }

        /// <summary>
        /// 测试相关入口 Update_ChangesMatchingRow；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、Sample、Update、ToList；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Update_ChangesMatchingRow()
        {
            var helper = CreateHelper();
            helper.Write(Sample(3));

            helper.Update<PersonRecord>(x => x.Name == "P2", new PersonRecord { Name = "P2", Age = 88, CreateTime = new DateTime(2026, 3, 3) });

            var result = helper.Query<PersonRecord>().ToList();
            Assert.Equal(88, result.First(x => x.Name == "P2").Age);
        }

        /// <summary>
        /// 测试相关入口 Update_WhenFileMissing_DoesNothing；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Update、Empty、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Update_WhenFileMissing_DoesNothing()
        {
            var helper = CreateHelper("none.xlsx");
            helper.Update<PersonRecord>(x => x.Name == "P1", new PersonRecord());
            Assert.Empty(helper.Query<PersonRecord>());
        }

        /// <summary>
        /// Level 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        private enum Level { Low, High }

        /// <summary>
        /// TypedRecord 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        private class TypedRecord
        {
            public string Name { get; set; } = string.Empty;
            public int? Score { get; set; }
            public Level Level { get; set; }
            public DateTime? When { get; set; }
        }

        /// <summary>
        /// 测试相关入口 WriteThenQuery_NullableEnumDate_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateHelper、Write、ToList、OrderBy、Query；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WriteThenQuery_NullableEnumDate_RoundTrip()
        {
            var helper = CreateHelper("typed.xlsx");
            helper.Write(new[]
            {
                new TypedRecord { Name = "A", Score = null, Level = Level.High, When = new DateTime(2026, 2, 3) },
                new TypedRecord { Name = "B", Score = 95, Level = Level.Low, When = null },
            });

            var result = helper.Query<TypedRecord>().OrderBy(x => x.Name).ToList();

            Assert.Equal(2, result.Count);
            Assert.Null(result[0].Score);
            Assert.Equal(Level.High, result[0].Level);
            Assert.Equal(new DateTime(2026, 2, 3), result[0].When);
            Assert.Equal(95, result[1].Score);
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
