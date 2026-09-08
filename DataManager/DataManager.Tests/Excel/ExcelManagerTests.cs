using DataManager.Excel;
using DataManager.Excel.Definition;
using DataManager.Excel.EPPlus;
using DataManager.Excel.MiniExcel;

namespace DataManager.Tests.Excel
{
    public class ExcelManagerTests
    {
        private static ExcelParam CreateParam(string name, ExcelType type) => new()
        {
            Name = name,
            FilePath = Path.Combine(Path.GetTempPath(), "dm_tests_excel"),
            FileName = $"{name}.xlsx",
            ToolType = type,
        };

        [Fact]
        public void Create_MiniExcel_ReturnsMiniExcelHelper()
        {
            var manager = new ExcelManager();
            var excel = manager.Create(CreateParam("m1", ExcelType.MiniExcel));

            Assert.IsType<MiniExcelHelper>(excel);
            Assert.NotNull(manager["m1"]);
        }

        [Fact]
        public void Create_EPPlus_ReturnsEPPlusHelper()
        {
            var manager = new ExcelManager();
            var excel = manager.Create(CreateParam("e1", ExcelType.EPPlus));

            Assert.IsType<EPPlusHelper>(excel);
            Assert.NotNull(manager["e1"]);
        }

        [Fact]
        public void Create_UnsupportedType_Throws()
        {
            var manager = new ExcelManager();
            Assert.Throws<NotSupportedException>(
                () => manager.Create(CreateParam("n1", ExcelType.NPOI)));
        }

        [Fact]
        public void ExcelParam_HasDefaults()
        {
            var param = new ExcelParam();
            Assert.True(param.HasHeader);
            Assert.Equal(ExcelType.MiniExcel, param.ToolType);
            Assert.Equal(180, param.SaveDays);
        }

        [Fact]
        public void Create_MissingFilePath_ThrowsArgumentException()
        {
            var manager = new ExcelManager();
            var param = CreateParam("v1", ExcelType.EPPlus);
            param.FilePath = string.Empty;

            Assert.Throws<ArgumentException>(() => manager.Create(param));
        }

        [Fact]
        public void Create_DuplicateName_Throws()
        {
            var manager = new ExcelManager();
            manager.Create(CreateParam("dup", ExcelType.EPPlus));

            Assert.Throws<InvalidOperationException>(() => manager.Create(CreateParam("dup", ExcelType.MiniExcel)));
        }
    }
}
