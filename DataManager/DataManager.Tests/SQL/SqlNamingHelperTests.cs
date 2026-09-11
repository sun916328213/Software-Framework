using DataManager.SQL.Definition;

namespace DataManager.Tests.SQL
{
    public class SqlNamingHelperTests
    {
        [Theory]
        [InlineData("TestEntity", "testentity")]
        public void ConvertTableName_PostgreSQL_Lowercases(string input, string expected)
        {
            Assert.Equal(expected, SqlNamingHelper.ConvertTableName(input, ESQLType.PostgreSQL));
        }

        [Fact]
        public void ConvertTableName_MySQL_Lowercases()
        {
            Assert.Equal("testentity", SqlNamingHelper.ConvertTableName("TestEntity", ESQLType.MySQL));
        }

        [Fact]
        public void FormatTableName_MySQL_WrapsLowercaseWithBackticks()
        {
            Assert.Equal("`testentity`", SqlNamingHelper.FormatTableName("TestEntity", ESQLType.MySQL));
        }

        [Fact]
        public void FormatTableName_PostgreSQL_LowercaseNoQuotes()
        {
            Assert.Equal("testentity", SqlNamingHelper.FormatTableName("TestEntity", ESQLType.PostgreSQL));
        }

        [Fact]
        public void FormatColumnName_MySQL_WrapsLowercaseWithBackticks()
        {
            Assert.Equal("`username`", SqlNamingHelper.FormatColumnName("UserName", ESQLType.MySQL));
        }

        [Fact]
        public void FormatColumnList_JoinsFormattedColumns()
        {
            var result = SqlNamingHelper.FormatColumnList(new[] { "Id", "Name" }, ESQLType.MySQL);
            Assert.Equal("`id`, `name`", result);
        }

        [Fact]
        public void FormatColumnList_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, SqlNamingHelper.FormatColumnList(Array.Empty<string>(), ESQLType.MySQL));
        }

        [Fact]
        public void NeedsConversion_AlwaysTrue()
        {
            Assert.True(SqlNamingHelper.NeedsConversion(ESQLType.PostgreSQL));
            Assert.True(SqlNamingHelper.NeedsConversion(ESQLType.MySQL));
        }

        [Fact]
        public void ConvertTableName_NullOrEmpty_ReturnsAsIs()
        {
            Assert.Null(SqlNamingHelper.ConvertTableName(null!, ESQLType.PostgreSQL));
            Assert.Equal(string.Empty, SqlNamingHelper.ConvertTableName(string.Empty, ESQLType.PostgreSQL));
        }
    }
}
