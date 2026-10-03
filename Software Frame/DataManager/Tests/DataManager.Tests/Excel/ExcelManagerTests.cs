// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ExcelManagerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Excel、DataManager.Runtime.Excel、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.EPPlus、DataManager.Runtime.Excel.MiniExcel。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.EPPlus;
using DataManager.Runtime.Excel.MiniExcel;

namespace DataManager.Tests.Excel
{
    /// <summary>
    /// ExcelManagerTests 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public class ExcelManagerTests
    {
        /// <summary>
        /// 验证 CreateParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Combine、GetTempPath；具体条件和先后次序见方法体。
        /// </summary>
        private static ExcelParam CreateParam(string name, ExcelType type) => new()
        {
            Name = name,
            FilePath = Path.Combine(Path.GetTempPath(), "dm_tests_excel"),
            FileName = $"{name}.xlsx",
            ToolType = type,
        };

        /// <summary>
        /// 验证 Create_MiniExcel_ReturnsMiniExcelHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、IsType、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_MiniExcel_ReturnsMiniExcelHelper()
        {
            var manager = new ExcelManager();
            var excel = manager.Create(CreateParam("m1", ExcelType.MiniExcel));

            Assert.IsType<MiniExcelHelper>(excel);
            Assert.NotNull(manager["m1"]);
        }

        /// <summary>
        /// 验证 Create_EPPlus_ReturnsEPPlusHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、IsType、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_EPPlus_ReturnsEPPlusHelper()
        {
            var manager = new ExcelManager();
            var excel = manager.Create(CreateParam("e1", ExcelType.EPPlus));

            Assert.IsType<EPPlusHelper>(excel);
            Assert.NotNull(manager["e1"]);
        }

        /// <summary>
        /// 验证 Create_UnsupportedType_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Create、CreateParam；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_UnsupportedType_Throws()
        {
            var manager = new ExcelManager();
            Assert.Throws<NotSupportedException>(
                () => manager.Create(CreateParam("n1", ExcelType.NPOI)));
        }

        /// <summary>
        /// 验证 ExcelParam_HasDefaults 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 True、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ExcelParam_HasDefaults()
        {
            var param = new ExcelParam();
            Assert.True(param.HasHeader);
            Assert.Equal(ExcelType.MiniExcel, param.ToolType);
            Assert.Equal(180, param.SaveDays);
        }

        /// <summary>
        /// 验证 Create_MissingFilePath_ThrowsArgumentException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateParam、Throws、Create；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_MissingFilePath_ThrowsArgumentException()
        {
            var manager = new ExcelManager();
            var param = CreateParam("v1", ExcelType.EPPlus);
            param.FilePath = string.Empty;

            Assert.Throws<ArgumentException>(() => manager.Create(param));
        }

        /// <summary>
        /// 验证 Create_DuplicateName_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_DuplicateName_Throws()
        {
            var manager = new ExcelManager();
            manager.Create(CreateParam("dup", ExcelType.EPPlus));

            Assert.Throws<InvalidOperationException>(() => manager.Create(CreateParam("dup", ExcelType.MiniExcel)));
        }
    }
}
