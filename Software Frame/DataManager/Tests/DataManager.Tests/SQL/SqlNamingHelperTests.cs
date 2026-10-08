// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlNamingHelperTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// SqlNamingHelperTests 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public class SqlNamingHelperTests
    {
        /// <summary>
        /// 测试相关入口 ConvertTableName_PostgreSQL_Lowercases；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ConvertTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData("TestEntity", "testentity")]
        public void ConvertTableName_PostgreSQL_Lowercases(string input, string expected)
        {
            Assert.Equal(expected, SqlNamingHelper.ConvertTableName(input, ESQLType.PostgreSQL));
        }

        /// <summary>
        /// 测试相关入口 ConvertTableName_MySQL_Lowercases；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ConvertTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ConvertTableName_MySQL_Lowercases()
        {
            Assert.Equal("testentity", SqlNamingHelper.ConvertTableName("TestEntity", ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 FormatTableName_MySQL_WrapsLowercaseWithBackticks；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FormatTableName_MySQL_WrapsLowercaseWithBackticks()
        {
            Assert.Equal("`testentity`", SqlNamingHelper.FormatTableName("TestEntity", ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 FormatTableName_PostgreSQL_LowercaseNoQuotes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FormatTableName_PostgreSQL_LowercaseNoQuotes()
        {
            Assert.Equal("testentity", SqlNamingHelper.FormatTableName("TestEntity", ESQLType.PostgreSQL));
        }

        /// <summary>
        /// 测试相关入口 FormatColumnName_MySQL_WrapsLowercaseWithBackticks；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、FormatColumnName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FormatColumnName_MySQL_WrapsLowercaseWithBackticks()
        {
            Assert.Equal("`username`", SqlNamingHelper.FormatColumnName("UserName", ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 FormatColumnList_JoinsFormattedColumns；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FormatColumnList、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FormatColumnList_JoinsFormattedColumns()
        {
            var result = SqlNamingHelper.FormatColumnList(new[] { "Id", "Name" }, ESQLType.MySQL);
            Assert.Equal("`id`, `name`", result);
        }

        /// <summary>
        /// 测试相关入口 FormatColumnList_Empty_ReturnsEmpty；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、FormatColumnList、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FormatColumnList_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, SqlNamingHelper.FormatColumnList(Array.Empty<string>(), ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 NeedsConversion_AlwaysTrue；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、NeedsConversion；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void NeedsConversion_AlwaysTrue()
        {
            Assert.True(SqlNamingHelper.NeedsConversion(ESQLType.PostgreSQL));
            Assert.True(SqlNamingHelper.NeedsConversion(ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 ConvertTableName_NullOrEmpty_ReturnsAsIs；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、ConvertTableName、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ConvertTableName_NullOrEmpty_ReturnsAsIs()
        {
            Assert.Null(SqlNamingHelper.ConvertTableName(null!, ESQLType.PostgreSQL));
            Assert.Equal(string.Empty, SqlNamingHelper.ConvertTableName(string.Empty, ESQLType.PostgreSQL));
        }
    }
}
