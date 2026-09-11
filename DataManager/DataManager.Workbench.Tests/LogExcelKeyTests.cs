using DataManager.Excel.Definition;
using DataManager.Log.Definition;
using DataManager.Workbench.Services;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// Log/Excel 连接指纹回归测试（与 SqlViewModelKeyTests 同款：配置变更必须重建实例）。
    /// </summary>
    public class LogExcelKeyTests
    {
        private static LogParam BaseLogParam() => new()
        {
            Name = "workbench",
            FilePath = @"C:\logs",
            Separator = "||",
            SaveDays = 180,
            LogType = ELogType.SeriLog,
        };

        private static ExcelParam BaseExcelParam() => new()
        {
            Name = "workbench",
            FilePath = @"C:\excel",
            FileName = "workbench.xlsx",
            HasHeader = true,
            ToolType = ExcelType.MiniExcel,
        };

        [Fact]
        public void BuildLogKey_SameParams_SameKey()
        {
            Assert.Equal(LogWorkspace.BuildLogKey(BaseLogParam()), LogWorkspace.BuildLogKey(BaseLogParam()));
        }

        [Theory]
        [InlineData("FilePath", @"D:\other")]
        [InlineData("Separator", "##")]
        [InlineData("Name", "other")]
        public void BuildLogKey_FieldChange_DifferentKey(string field, object value)
        {
            var before = BaseLogParam();
            var after = BaseLogParam();
            typeof(LogParam).GetProperty(field)!.SetValue(after, value);

            Assert.NotEqual(LogWorkspace.BuildLogKey(before), LogWorkspace.BuildLogKey(after));
        }

        [Fact]
        public void BuildLogKey_LogTypeChange_DifferentKey()
        {
            var before = BaseLogParam();
            var after = BaseLogParam();
            after.LogType = ELogType.NLog;

            Assert.NotEqual(LogWorkspace.BuildLogKey(before), LogWorkspace.BuildLogKey(after));
        }

        [Fact]
        public void BuildExcelKey_SameParams_SameKey()
        {
            Assert.Equal(ExcelWorkspace.BuildExcelKey(BaseExcelParam()), ExcelWorkspace.BuildExcelKey(BaseExcelParam()));
        }

        [Theory]
        [InlineData("FileName", "other.xlsx")]
        [InlineData("FilePath", @"D:\other")]
        public void BuildExcelKey_FieldChange_DifferentKey(string field, object value)
        {
            var before = BaseExcelParam();
            var after = BaseExcelParam();
            typeof(ExcelParam).GetProperty(field)!.SetValue(after, value);

            Assert.NotEqual(ExcelWorkspace.BuildExcelKey(before), ExcelWorkspace.BuildExcelKey(after));
        }

        [Fact]
        public void BuildExcelKey_ToolTypeChange_DifferentKey()
        {
            var before = BaseExcelParam();
            var after = BaseExcelParam();
            after.ToolType = ExcelType.EPPlus;

            Assert.NotEqual(ExcelWorkspace.BuildExcelKey(before), ExcelWorkspace.BuildExcelKey(after));
        }
    }
}
