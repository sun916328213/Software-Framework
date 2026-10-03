// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogExcelKeyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Workbench.Models、DataManager.Core.Excel.Definition、DataManager.Core.Log.Definition、DataManager.Controls.Services。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Workbench.Models;
using DataManager.Core.Excel.Definition;
using DataManager.Core.Log.Definition;
using DataManager.Controls.Services;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// Log/Excel 连接指纹回归测试（与 SqlViewModelKeyTests 同款：配置变更必须重建实例）。
    /// </summary>
    public class LogExcelKeyTests
    {
        /// <summary>
        /// 验证 BaseLogParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private static LogParam BaseLogParam() => new()
        {
            Name = "workbench",
            FilePath = @"C:\logs",
            Separator = "||",
            SaveDays = 180,
            LogType = ELogType.SeriLog,
        };

        /// <summary>
        /// 验证 BaseExcelParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private static ExcelParam BaseExcelParam() => new()
        {
            Name = "workbench",
            FilePath = @"C:\excel",
            FileName = "workbench.xlsx",
            HasHeader = true,
            ToolType = ExcelType.MiniExcel,
        };

        /// <summary>
        /// 验证 BuildLogKey_SameParams_SameKey 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、BuildLogKey、BaseLogParam；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildLogKey_SameParams_SameKey()
        {
            Assert.Equal(LogWorkspace.BuildLogKey(BaseLogParam()), LogWorkspace.BuildLogKey(BaseLogParam()));
        }

        /// <summary>
        /// 验证 BuildLogKey_FieldChange_DifferentKey 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BaseLogParam、SetValue、GetProperty、NotEqual、BuildLogKey；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 BuildLogKey_LogTypeChange_DifferentKey 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BaseLogParam、NotEqual、BuildLogKey；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildLogKey_LogTypeChange_DifferentKey()
        {
            var before = BaseLogParam();
            var after = BaseLogParam();
            after.LogType = ELogType.NLog;

            Assert.NotEqual(LogWorkspace.BuildLogKey(before), LogWorkspace.BuildLogKey(after));
        }

        /// <summary>
        /// 验证 BuildExcelKey_SameParams_SameKey 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、BuildExcelKey、BaseExcelParam；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildExcelKey_SameParams_SameKey()
        {
            Assert.Equal(ExcelWorkspace<ExcelPerson>.BuildExcelKey(BaseExcelParam()), ExcelWorkspace<ExcelPerson>.BuildExcelKey(BaseExcelParam()));
        }

        /// <summary>
        /// 验证 BuildExcelKey_FieldChange_DifferentKey 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BaseExcelParam、SetValue、GetProperty、NotEqual、BuildExcelKey；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData("FileName", "other.xlsx")]
        [InlineData("FilePath", @"D:\other")]
        public void BuildExcelKey_FieldChange_DifferentKey(string field, object value)
        {
            var before = BaseExcelParam();
            var after = BaseExcelParam();
            typeof(ExcelParam).GetProperty(field)!.SetValue(after, value);

            Assert.NotEqual(ExcelWorkspace<ExcelPerson>.BuildExcelKey(before), ExcelWorkspace<ExcelPerson>.BuildExcelKey(after));
        }

        /// <summary>
        /// 验证 BuildExcelKey_ToolTypeChange_DifferentKey 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BaseExcelParam、NotEqual、BuildExcelKey；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildExcelKey_ToolTypeChange_DifferentKey()
        {
            var before = BaseExcelParam();
            var after = BaseExcelParam();
            after.ToolType = ExcelType.EPPlus;

            Assert.NotEqual(ExcelWorkspace<ExcelPerson>.BuildExcelKey(before), ExcelWorkspace<ExcelPerson>.BuildExcelKey(after));
        }
    }
}
