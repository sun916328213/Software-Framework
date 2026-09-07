using DataManager.SQL.Dapper;
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
        public void ConvertTableName_MySQL_KeepsCase()
        {
            Assert.Equal("TestEntity", SqlNamingHelper.ConvertTableName("TestEntity", ESQLType.MySQL));
        }

        [Fact]
        public void FormatTableName_MySQL_WrapsWithBackticks()
        {
            Assert.Equal("`TestEntity`", SqlNamingHelper.FormatTableName("TestEntity", ESQLType.MySQL));
        }

        [Fact]
        public void FormatTableName_PostgreSQL_LowercaseNoQuotes()
        {
            Assert.Equal("testentity", SqlNamingHelper.FormatTableName("TestEntity", ESQLType.PostgreSQL));
        }

        [Fact]
        public void FormatColumnName_MySQL_WrapsWithBackticks()
        {
            Assert.Equal("`UserName`", SqlNamingHelper.FormatColumnName("UserName", ESQLType.MySQL));
        }

        [Fact]
        public void FormatColumnList_JoinsFormattedColumns()
        {
            var result = SqlNamingHelper.FormatColumnList(new[] { "Id", "Name" }, ESQLType.MySQL);
            Assert.Equal("`Id`, `Name`", result);
        }

        [Fact]
        public void FormatColumnList_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, SqlNamingHelper.FormatColumnList(Array.Empty<string>(), ESQLType.MySQL));
        }

        [Fact]
        public void NeedsConversion_TrueOnlyForPostgreSQL()
        {
            Assert.True(SqlNamingHelper.NeedsConversion(ESQLType.PostgreSQL));
            Assert.False(SqlNamingHelper.NeedsConversion(ESQLType.MySQL));
        }

        [Fact]
        public void ConvertTableName_NullOrEmpty_ReturnsAsIs()
        {
            Assert.Null(SqlNamingHelper.ConvertTableName(null, ESQLType.PostgreSQL));
            Assert.Equal(string.Empty, SqlNamingHelper.ConvertTableName(string.Empty, ESQLType.PostgreSQL));
        }
    }
}
