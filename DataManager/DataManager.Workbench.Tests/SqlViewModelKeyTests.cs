using DataManager.Workbench.Models;
using DataManager.SQL.Definition;
using DataManager.Controls.Services;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// SqlViewModel 连接指纹回归测试：密码纠错后必须重建实例。
    /// 背景：键曾只含“工具+库类型”，密码改对后仍复用旧实例，一直报密码错误。
    /// 指纹按 DataManager 分字段口径计算（工作台档案标准串先转分字段再进指纹）。
    /// </summary>
    public class SqlViewModelKeyTests
    {
        private static SQLParam BaseParam() => new()
        {
            Name = "workbench",
            ServerIP = "127.0.0.1",
            Port = "5432",
            UserName = "postgres",
            Password = "wrong",
            DBName = "dm_integration",
            DBType = ESQLType.PostgreSQL,
            ToolType = ESQLToolType.Dapper,
        };

        [Fact]
        public void BuildDbKey_PasswordChange_ProducesDifferentKey()
        {
            var before = BaseParam();
            var after = BaseParam();
            after.Password = "123";

            Assert.NotEqual(SqlWorkspace<TestEntity>.BuildDbKey(before), SqlWorkspace<TestEntity>.BuildDbKey(after));
        }

        [Fact]
        public void BuildDbKey_SameParams_ProducesSameKey()
        {
            Assert.Equal(SqlWorkspace<TestEntity>.BuildDbKey(BaseParam()), SqlWorkspace<TestEntity>.BuildDbKey(BaseParam()));
        }

        [Theory]
        [InlineData("ServerIP", "192.168.1.10")]
        [InlineData("Port", "5433")]
        [InlineData("UserName", "admin")]
        [InlineData("DBName", "other")]
        [InlineData("FilePath", "other.db")]
        [InlineData("ConnectionString", "Server=x;")]
        public void BuildDbKey_AnyFieldChange_ProducesDifferentKey(string field, string value)
        {
            var before = BaseParam();
            var after = BaseParam();
            typeof(SQLParam).GetProperty(field)!.SetValue(after, value);

            Assert.NotEqual(SqlWorkspace<TestEntity>.BuildDbKey(before), SqlWorkspace<TestEntity>.BuildDbKey(after));
        }

        [Fact]
        public void BuildDbKey_TrustServerCertificateChange_ProducesDifferentKey()
        {
            var before = BaseParam();
            var after = BaseParam();
            after.TrustServerCertificate = !before.TrustServerCertificate;

            Assert.NotEqual(SqlWorkspace<TestEntity>.BuildDbKey(before), SqlWorkspace<TestEntity>.BuildDbKey(after));
        }
    }
}
