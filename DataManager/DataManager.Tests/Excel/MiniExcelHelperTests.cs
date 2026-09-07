using DataManager.Excel.Definition;
using DataManager.Excel.MiniExcel;

namespace DataManager.Tests.Excel
{
    /// <summary>
    /// MiniExcelHelper 功能测试（基于临时目录，无需数据库）
    /// </summary>
    public class MiniExcelHelperTests : IDisposable
    {
        private readonly string _dir;

        public MiniExcelHelperTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dm_tests_miniexcel", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

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

        private static List<PersonRecord> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new PersonRecord { Name = $"M{i}", Age = 30 + i, CreateTime = new DateTime(2026, 1, i) })
            .ToList();

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

        [Fact]
        public void Query_WithPredicate_Filters()
        {
            var helper = CreateHelper();
            helper.Write(Sample(5));

            var result = helper.Query<PersonRecord>(x => x.Age > 33).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task QueryAsync_ReturnsData()
        {
            var helper = CreateHelper();
            helper.Write(Sample(2));

            var result = await helper.QueryAsync<PersonRecord>();

            Assert.Equal(2, result.Count());
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
