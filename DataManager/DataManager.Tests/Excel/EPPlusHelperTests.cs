using DataManager.Excel.Definition;
using DataManager.Excel.EPPlus;

namespace DataManager.Tests.Excel
{
    /// <summary>
    /// EPPlusHelper 功能测试（基于临时目录，无需数据库）
    /// </summary>
    public class EPPlusHelperTests : IDisposable
    {
        private readonly string _dir;

        public EPPlusHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_epplus", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

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

        private static List<PersonRecord> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new PersonRecord { Name = $"P{i}", Age = 20 + i, CreateTime = new DateTime(2026, 1, i) })
            .ToList();

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

        [Fact]
        public void Query_WhenFileMissing_ReturnsEmpty()
        {
            var helper = CreateHelper("missing.xlsx");
            Assert.Empty(helper.Query<PersonRecord>());
        }

        [Fact]
        public void Query_WithPredicate_Filters()
        {
            var helper = CreateHelper();
            helper.Write(Sample(5));

            var result = helper.Query<PersonRecord>(x => x.Age > 23).ToList();

            Assert.All(result, x => Assert.True(x.Age > 23));
            Assert.Equal(2, result.Count);
        }

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

        [Fact]
        public void Update_ChangesMatchingRow()
        {
            var helper = CreateHelper();
            helper.Write(Sample(3));

            helper.Update<PersonRecord>(x => x.Name == "P2", new PersonRecord { Name = "P2", Age = 88, CreateTime = new DateTime(2026, 3, 3) });

            var result = helper.Query<PersonRecord>().ToList();
            Assert.Equal(88, result.First(x => x.Name == "P2").Age);
        }

        [Fact]
        public void Update_WhenFileMissing_DoesNothing()
        {
            var helper = CreateHelper("none.xlsx");
            helper.Update<PersonRecord>(x => x.Name == "P1", new PersonRecord());
            Assert.Empty(helper.Query<PersonRecord>());
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
